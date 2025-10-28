using MongoDB.Driver;
using PoC.OutboxMongoDb;

var builder = Host.CreateApplicationBuilder(args);
// builder.Services.AddHostedService<ProcessOutboxWorker>();
//builder.Services.AddHostedService<SampleDataOutbox>();
builder.Services.AddHostedService<OutboxMessageSchemes>();

builder.Services.AddSingleton<IMongoClient>(e =>
{
    var mongoConnectionString = builder.Configuration.GetValue<string>("MongoDb:ConnectionString");
    return new MongoClient(mongoConnectionString);
});
var host = builder.Build();
host.Run();
