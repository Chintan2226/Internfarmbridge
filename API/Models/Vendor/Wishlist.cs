using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Admin;

namespace API.Models.Vendor
{
    public class Wishlist
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "VendorId is required.")]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public VendorProfile? Vendor { get; set; }

        [Required(ErrorMessage = "CatalogProductId is required.")]
        public int CatalogProductId { get; set; }

        [ForeignKey("CatalogProductId")]
        public CatalogProduct? CatalogProduct { get; set; }

        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
    }
}