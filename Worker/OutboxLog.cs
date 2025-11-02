namespace Worker;

public class OutboxLog
{
    public string Id { get; set; }
    public User Data { get; set; }
    public OutboxStatus OutboxStatus { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public enum OutboxStatus
{
    Pending,
    Processed,
    Failed
}
