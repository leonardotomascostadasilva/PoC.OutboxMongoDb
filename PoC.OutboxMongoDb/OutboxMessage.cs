
namespace PoC.OutboxMongoDb
{
    public sealed class OutboxMessage
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public object Data { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LockedUntil { get; set; }
    }

    public sealed class OutboxProcessLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string MessageId { get; set; } = default!;
        public string Instance { get; set; } = default!;
        public DateTime AttemptAt { get; set; }
        public bool Succeeded { get; set; }
    }
}
