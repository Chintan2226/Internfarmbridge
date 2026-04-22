namespace API.DTOs
{
    public class NotificationMessageDto
    {
        public int UserId { get; set; }
        public string Role { get; set; } = "";
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public string Type { get; set; } = "";
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}