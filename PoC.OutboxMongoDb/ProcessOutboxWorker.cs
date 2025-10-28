using MongoDB.Bson;
using MongoDB.Driver;

namespace PoC.OutboxMongoDb
{
    public class ProcessOutboxWorker(IMongoClient mongoClient) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var database = mongoClient.GetDatabase("OutboxDemoDb");
            var outboxCollection = database.GetCollection<BsonDocument>("OutboxMessages");

            await outboxCollection.InsertOneAsync(new BsonDocument
            {
                { "status", 0 },
                { "createdAt", DateTime.UtcNow },
                { "lockHistory", new BsonArray() }
            }, stoppingToken);

            // Simula duas instâncias concorrentes
            var task1 = ProcessOutbox(outboxCollection, "Instance A");
            var task2 = ProcessOutbox(outboxCollection, "Instance B");
            var task3 = ProcessOutbox(outboxCollection, "Instance C");
            var task4 = ProcessOutbox(outboxCollection, "Instance D");
            await Task.WhenAll(task1, task2, task3, task4);
        }

        public async Task ProcessOutbox(IMongoCollection<BsonDocument> collection, string instanceName)
        {
            var now = DateTime.UtcNow;
            var lockedUntil = now.AddMinutes(1);

            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("status", 0),
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("lockedUnitl", BsonNull.Value),
                    Builders<BsonDocument>.Filter.Lte("lockedUnitl", now),
                    Builders<BsonDocument>.Filter.Exists("lockedUnitl", false)
                )
            );

            var update = Builders<BsonDocument>.Update
               .Set("status", 1)
               .Set("lockedUnitl", lockedUntil)
               .Push("lockHistory", new BsonDocument
               {
                    { "by", instanceName },
                    { "at", now },
                    { "success", true }
               });

            var result = await collection.UpdateOneAsync(filter, update);

            Console.WriteLine($"{instanceName}: Matched={result.MatchedCount}, Modified={result.ModifiedCount}");
        }

    }
}
