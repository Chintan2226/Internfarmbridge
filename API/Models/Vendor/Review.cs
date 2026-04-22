using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Admin;

namespace API.Models.Vendor
{
    public class Review
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "VendorId is required.")]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public VendorProfile? Vendor { get; set; }

        [Required(ErrorMessage = "OrderId is required.")]
        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public VendorOrder? Order { get; set; }

        [Required(ErrorMessage = "Rating is required.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int Rating { get; set; }

        [MaxLength(2000, ErrorMessage = "Review text must not exceed 2000 characters.")]
        [MinLength(10, ErrorMessage = "Review text must be at least 10 characters.")]
        public string? ReviewText { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters.")]
        [RegularExpression("^(Pending|Approved|Rejected)$",
            ErrorMessage = "Status must be Pending, Approved, or Rejected.")]
        public string Status { get; set; } = "Pending";

        public int? ModeratedBy { get; set; }

        [ForeignKey("ModeratedBy")]
        public AdminProfile? ModeratedByAdmin { get; set; }

        public DateTime? ModeratedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}