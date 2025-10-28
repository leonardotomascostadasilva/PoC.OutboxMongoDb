using MongoDB.Driver;

namespace PoC.OutboxMongoDb
{
    public class OutboxMessageSchemes(IMongoClient mongoClient) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var tasks = Enumerable.Range(1, 10)
                .Select(i => ProcessOutbox($"Instance {i}", stoppingToken))
                .ToArray();

            await Task.WhenAll(tasks);
        }

        private async Task ProcessOutbox(string instanceName, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var database = mongoClient.GetDatabase("OutboxDemoDb");
                var outboxCollection = database.GetCollection<OutboxMessage>("OutboxMessages");
                var logCollection = database.GetCollection<OutboxProcessLog>("OutboxProcessLog");

                var now = DateTime.UtcNow;
                var lockedUntil = now.AddMinutes(1);
                var filter = Builders<OutboxMessage>.Filter.And(
                    Builders<OutboxMessage>.Filter.Eq(e => e.Status, "Pending"),
                    Builders<OutboxMessage>.Filter.Or(
                        Builders<OutboxMessage>.Filter.Eq(e => e.LockedUntil, null),
                        Builders<OutboxMessage>.Filter.Lte(e => e.LockedUntil, now),
                        Builders<OutboxMessage>.Filter.Exists(e => e.LockedUntil, false)
                    )
                );

                var availableMessages = await outboxCollection
                    .Find(filter)
                    .SortBy(e => e.CreatedAt)
                    .Limit(100)
                    .ToListAsync(cancellationToken);

                if (availableMessages.Count == 0)
                {
                    Console.WriteLine($"{instanceName}: No messages to process.");
                    await Task.Delay(1000, cancellationToken);

                    return;
                }

                foreach (var msg in availableMessages)
                {
                    var update = Builders<OutboxMessage>.Update
                        .Set(e => e.Status, "Processed")
                        .Set(e => e.LockedUntil, lockedUntil);

                    var specificFilter = Builders<OutboxMessage>.Filter.And(
                        Builders<OutboxMessage>.Filter.Eq(e => e.Id, msg.Id),
                        Builders<OutboxMessage>.Filter.Eq(e => e.Status, "Pending"),
                        Builders<OutboxMessage>.Filter.Or(
                            Builders<OutboxMessage>.Filter.Eq(e => e.LockedUntil, null),
                            Builders<OutboxMessage>.Filter.Lte(e => e.LockedUntil, now),
                            Builders<OutboxMessage>.Filter.Exists(e => e.LockedUntil, false)
                        )
                    );

                    var result = await outboxCollection.UpdateOneAsync(
                        specificFilter,
                        update,
                        cancellationToken: cancellationToken
                    );

                    if (result.ModifiedCount > 0)
                    {
                        await logCollection.InsertOneAsync(new OutboxProcessLog
                        {
                            MessageId = msg.Id,
                            Instance = instanceName,
                            AttemptAt = DateTime.UtcNow,
                            Succeeded = true
                        }, cancellationToken: cancellationToken);

                        Console.WriteLine($"{instanceName}: Processed message {msg.Id}");
                    }
                }
            }
        }
    }
}
