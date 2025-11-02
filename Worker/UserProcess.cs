using MongoDB.Driver;

namespace Worker
{
    public class UserProcess(IMongoClient mongoClient) : BackgroundService
    {

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var tasks = Enumerable.Range(1, 2)
                .Select(i => ProcessOutbox($"Instance {i}", stoppingToken))
                .ToArray();

                await Task.WhenAll(tasks);
            }
        }

        private async Task ProcessOutbox(string instanceName, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var database = mongoClient.GetDatabase("Test");
                var outboxCollection = database.GetCollection<OutboxLog>("OutboxLogs");
                var logCollection = database.GetCollection<UserEvent>("UserEvents");



                var now = DateTime.UtcNow;
                var lockedUntil = now.AddMinutes(1);
                var filter = Builders<OutboxLog>.Filter.And(
                    Builders<OutboxLog>.Filter.Eq(e => e.OutboxStatus, OutboxStatus.Pending),
                    Builders<OutboxLog>.Filter.Lt(e => e.Attempts, 3),
                    Builders<OutboxLog>.Filter.Or(
                        Builders<OutboxLog>.Filter.Eq(e => e.UpdatedAt, null),
                        Builders<OutboxLog>.Filter.Lte(e => e.UpdatedAt, now),
                        Builders<OutboxLog>.Filter.Exists(e => e.UpdatedAt, false)
                    )
                );

                var availableMessages = await outboxCollection
                    .Find(filter)
                    .SortBy(e => e.CreatedAt)
                    .Limit(10000)
                    .ToListAsync(cancellationToken);

                if (availableMessages.Count == 0)
                {
                    Console.WriteLine($"{instanceName}: No messages to process.");
                    await Task.Delay(1000, cancellationToken);

                    return;
                }


                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(availableMessages, parallelOptions, async (msg, token) =>
                {
                    try
                    {
                        var update = Builders<OutboxLog>.Update
                        .Set(e => e.OutboxStatus, OutboxStatus.Processed)
                        .Set(e => e.UpdatedAt, lockedUntil);

                        var specificFilter = Builders<OutboxLog>.Filter.And(
                            Builders<OutboxLog>.Filter.Eq(e => e.Id, msg.Id),
                            Builders<OutboxLog>.Filter.Eq(e => e.OutboxStatus, OutboxStatus.Pending),
                            Builders<OutboxLog>.Filter.Lt(e => e.Attempts, 3),
                            Builders<OutboxLog>.Filter.Or(
                                Builders<OutboxLog>.Filter.Eq(e => e.UpdatedAt, null),
                                Builders<OutboxLog>.Filter.Lte(e => e.UpdatedAt, now),
                                Builders<OutboxLog>.Filter.Exists(e => e.UpdatedAt, false)
                            )
                        );

                        var result = await outboxCollection.UpdateOneAsync(
                            specificFilter,
                            update,
                            cancellationToken: cancellationToken
                        );

                        if (result.ModifiedCount > 0)
                        {
                            await logCollection.InsertOneAsync(new UserEvent
                            {
                                Id = Guid.NewGuid().ToString(),
                                UserId = msg.Data.Id,
                                Status = msg.Data.Status,
                                CreatedAt = msg.Data.CreatedAt
                            }, cancellationToken: cancellationToken);

                            Console.WriteLine($"{instanceName}: Processed message {msg.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{instanceName}: Error occurred - {ex.Message}");
                        await outboxCollection.UpdateOneAsync(
                                    Builders<OutboxLog>.Filter.Eq(e => e.Id, msg.Id),
                                    Builders<OutboxLog>.Update
                                        .Inc(e => e.Attempts, 1)
                                        .Set(e => e.OutboxStatus, OutboxStatus.Pending)
                                        .Set(e => e.UpdatedAt, DateTime.UtcNow),
                                    cancellationToken: cancellationToken);
                    }
                });
            }
        }
    }
}
