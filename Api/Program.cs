using Api;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = MongoClientSettings.FromConnectionString(builder.Configuration.GetConnectionString("MongoDb")!);
    return new MongoClient(settings);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapPost("/user", async ([FromServices] IMongoClient mongoClient, CancellationToken cancellationToken) =>
{
    var database = mongoClient.GetDatabase("Test");
    var users = database.GetCollection<User>("Users");
    var outboxLog = database.GetCollection<OutboxLog>("OutboxLogs");

    var usersForInsert = new List<User>();
    var outboxEntriesForInsert = new List<OutboxLog>();
    for (var i = 0; i < 50_000; i++)
    {
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Status = Status.Active,
            CreatedAt = DateTime.UtcNow
        };
        var outboxEntry = new OutboxLog
        {
            Id = Guid.NewGuid().ToString(),
            Data = user,
            Attempts = 0,
            OutboxStatus = OutboxStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        usersForInsert.Add(user);
        outboxEntriesForInsert.Add(outboxEntry);
        
    }

    await Task.WhenAll(
            users.InsertManyAsync(usersForInsert, new InsertManyOptions(), cancellationToken),
            outboxLog.InsertManyAsync(outboxEntriesForInsert, new InsertManyOptions(), cancellationToken));

});

app.Run();
