using System.Text.Json.Serialization;

namespace API.Models.Notification
{
    public class vm_Notification
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string Type { get; set; } = "Info";
        public string? ReferenceType { get; set; }
        public int? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;

        // ✅ REQUIRED FOR RABBITMQ (NO JsonIgnore)
        public int TargetUserId { get; set; }
        public string TargetRole { get; set; } = "";
    }
}