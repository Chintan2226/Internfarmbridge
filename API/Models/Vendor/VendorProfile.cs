using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Auth;

namespace API.Models.Vendor
{
    public class VendorProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "UserId is required.")]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required(ErrorMessage = "Business name is required.")]
        [MaxLength(200, ErrorMessage = "Business name must not exceed 200 characters.")]
        [MinLength(2, ErrorMessage = "Business name must be at least 2 characters.")]
        public string BusinessName { get; set; } = "";

        [MaxLength(150, ErrorMessage = "Contact person must not exceed 150 characters.")]
        public string? ContactPerson { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [MaxLength(15, ErrorMessage = "Phone number must not exceed 15 characters.")]
        public string? Phone { get; set; }

        [MaxLength(15, ErrorMessage = "GSTIN must not exceed 15 characters.")]
        [RegularExpression(@"^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1}$",
            ErrorMessage = "Invalid GSTIN format.")]
        public string? Gstin { get; set; }

        [MaxLength(50, ErrorMessage = "Onboarding status must not exceed 50 characters.")]
        [RegularExpression("^(Pending|Approved|Rejected|UnderReview)$",
            ErrorMessage = "Onboarding status must be Pending, Approved, Rejected, or UnderReview.")]
        public string OnboardingStatus { get; set; } = "Pending";

        public DateTime? ApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}