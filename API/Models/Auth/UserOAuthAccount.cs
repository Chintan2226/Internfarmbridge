using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Auth
{
    public class UserOAuthAccount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "UserId is required.")]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required(ErrorMessage = "Provider is required.")]
        [MaxLength(50, ErrorMessage = "Provider must not exceed 50 characters.")]
        [RegularExpression("^(Google|Facebook|Apple|Microsoft)$",
            ErrorMessage = "Provider must be Google, Facebook, Apple, or Microsoft.")]
        public string Provider { get; set; } = "";

        [Required(ErrorMessage = "Provider user ID is required.")]
        [MaxLength(256, ErrorMessage = "Provider user ID must not exceed 256 characters.")]
        public string ProviderUserId { get; set; } = "";

        [EmailAddress(ErrorMessage = "Invalid provider email format.")]
        [MaxLength(256, ErrorMessage = "Provider email must not exceed 256 characters.")]
        public string? ProviderEmail { get; set; }

        [MaxLength(150, ErrorMessage = "Display name must not exceed 150 characters.")]
        public string? DisplayName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}