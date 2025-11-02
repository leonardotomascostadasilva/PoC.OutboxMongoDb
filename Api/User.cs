namespace Api
{
    public class User
    {
        public string Id { get; set; }
        public Status Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }


    public enum Status
    {
        Active,
        Inactive,
        Pending
    }
}
