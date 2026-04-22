using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Admin;

namespace API.Models.Farmer
{
    public class FarmerCropListing
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "FarmerId is required.")]
        public int FarmerId { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        [Required(ErrorMessage = "CatalogProductId is required.")]
        public int CatalogProductId { get; set; }

        [ForeignKey("CatalogProductId")]
        public CatalogProduct? CatalogProduct { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity available must be greater than 0.")]
        public decimal QuantityAvailable { get; set; }

        [Required(ErrorMessage = "Unit is required.")]
        [MaxLength(20, ErrorMessage = "Unit must not exceed 20 characters.")]
        [RegularExpression("^(kg|quintal|ton|bag)$",
            ErrorMessage = "Unit must be kg, quintal, ton, or bag.")]
        public string Unit { get; set; } = "";

        [MaxLength(100, ErrorMessage = "Variety must not exceed 100 characters.")]
        public string Variety { get; set; } = "";

        [Required(ErrorMessage = "Asking price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Asking price must be greater than 0.")]
        public decimal AskingPrice { get; set; }

        public DateTime? HarvestDate { get; set; }

        [MaxLength(500, ErrorMessage = "Farm address must not exceed 500 characters.")]
        public string? FarmAddress { get; set; }

        [MaxLength(100, ErrorMessage = "Farm state must not exceed 100 characters.")]
        public string? FarmState { get; set; }

        [MaxLength(100, ErrorMessage = "Farm district must not exceed 100 characters.")]
        public string? FarmDistrict { get; set; }

        [MaxLength(1000, ErrorMessage = "Notes must not exceed 1000 characters.")]
        public string? Notes { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters.")]
        [RegularExpression("^(Draft|Submitted|Approved|Rejected|Sold|QC Scheduled)$",
            ErrorMessage = "Invalid status value.")]
        public string Status { get; set; } = "Draft";

        public DateTime? SubmittedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}