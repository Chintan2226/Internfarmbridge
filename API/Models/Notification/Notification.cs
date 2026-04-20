using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Auth;

namespace API.Models.Notification
{
    public class Notification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "UserId is required.")]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required(ErrorMessage = "Type is required.")]
        [MaxLength(100, ErrorMessage = "Type must not exceed 100 characters.")]
        public string Type { get; set; } = "";

        [MaxLength(20, ErrorMessage = "Channel must not exceed 20 characters.")]
        [RegularExpression("^(InApp|Email|SMS|Push|system)$",
            ErrorMessage = "Channel must be InApp, Email, SMS, Push, or system.")]
        public string Channel { get; set; } = "InApp";

        [MaxLength(200, ErrorMessage = "Title must not exceed 200 characters.")]
        public string? Title { get; set; }

        [MaxLength(1000, ErrorMessage = "Body must not exceed 1000 characters.")]
        public string? Body { get; set; }

        [MaxLength(100, ErrorMessage = "Reference type must not exceed 100 characters.")]
        public string? ReferenceType { get; set; }

        public int? ReferenceId { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}