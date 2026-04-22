using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Vendor
{
    public class VendorDeliveryLocation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "VendorId is required.")]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public VendorProfile? Vendor { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        [MaxLength(500, ErrorMessage = "Address must not exceed 500 characters.")]
        public string Address { get; set; } = "";

        [Required(ErrorMessage = "City is required.")]
        [MaxLength(100, ErrorMessage = "City must not exceed 100 characters.")]
        public string City { get; set; } = "";

        [Required(ErrorMessage = "State is required.")]
        [MaxLength(100, ErrorMessage = "State must not exceed 100 characters.")]
        public string State { get; set; } = "";

        [Required(ErrorMessage = "Pincode is required.")]
        [MaxLength(6, ErrorMessage = "Pincode must not exceed 6 characters.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Pincode must be a 6-digit number.")]
        public string Pincode { get; set; } = "";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}