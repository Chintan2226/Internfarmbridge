using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Auth;

namespace API.Models.FieldOfficer
{
    public class FieldOfficerProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "UserId is required.")]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(150, ErrorMessage = "Full name must not exceed 150 characters.")]
        [MinLength(2, ErrorMessage = "Full name must be at least 2 characters.")]
        public string FullName { get; set; } = "";

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [MaxLength(15, ErrorMessage = "Phone number must not exceed 15 characters.")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "WarehouseId is required.")]
        public int WarehouseId { get; set; }

        [MaxLength(200, ErrorMessage = "Assigned region must not exceed 200 characters.")]
        public string? AssignedRegion { get; set; }

        [Url(ErrorMessage = "Profile image URL must be a valid URL.")]
        [MaxLength(2048, ErrorMessage = "Profile image URL must not exceed 2048 characters.")]
        public string? ProfileImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}