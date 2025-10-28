using MongoDB.Driver;

namespace PoC.OutboxMongoDb
{
    public class SampleDataOutbox(IMongoClient mongoClient) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var database = mongoClient.GetDatabase("OutboxDemoDb");
            var outboxCollection = database.GetCollection<OutboxMessage>("OutboxMessages");

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount, 
                CancellationToken = stoppingToken
            };

            Parallel.For(0, 100_000, parallelOptions, i =>
            {
                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid().ToString(),
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    Data = $"Sample data {i}"
                };
                outboxCollection.InsertOne(outboxMessage);
            });
        }
    }
}
