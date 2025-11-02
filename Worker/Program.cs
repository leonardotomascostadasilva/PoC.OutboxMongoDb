using MongoDB.Driver;
using Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = MongoClientSettings.FromConnectionString(builder.Configuration.GetConnectionString("MongoDb")!);
    return new MongoClient(settings);
});
builder.Services.AddHostedService<UserProcess>();

var host = builder.Build();
host.Run();
