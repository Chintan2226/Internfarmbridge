using System;
using System.Text.Json.Serialization;

namespace API.Models.Notification
{
    public class vm_Notification
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("message")]
        public string Message { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "Info"; // Ensure this matches "Info", "Critical", etc.

        [JsonPropertyName("category")]
        public string Category { get; set; } = "System";

        [JsonPropertyName("redirectUrl")]
        public string RedirectUrl { get; set; } = "";

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("isRead")]
        public bool IsRead { get; set; } = false;
    }
}