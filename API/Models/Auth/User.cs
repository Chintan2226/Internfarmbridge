using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Auth
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
        public string Email { get; set; } = "";

        [MinLength(8, ErrorMessage = "Password hash must be at least 8 characters.")]
        [MaxLength(512, ErrorMessage = "Password hash must not exceed 512 characters.")]
        public string PasswordHash { get; set; } = "";

        [Required(ErrorMessage = "Role is required.")]
        [MaxLength(50, ErrorMessage = "Role must not exceed 50 characters.")]
        [RegularExpression("^(Admin|Farmer|Vendor|FieldOfficer|farmer|vendor)$", ErrorMessage = "Role must be Admin, Farmer, Vendor, or FieldOfficer.")]
        public string Role { get; set; } = "";

        [Url(ErrorMessage = "Profile image URL must be a valid URL.")]
        [MaxLength(2048, ErrorMessage = "Profile image URL must not exceed 2048 characters.")]
        public string? ProfileImageUrl { get; set; }

        [MaxLength(10, ErrorMessage = "Language preference must not exceed 10 characters.")]
        public string? LanguagePreference { get; set; }

        public bool IsFirstLogin { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public bool IsApproved { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}