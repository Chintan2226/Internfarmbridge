using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.FieldOfficer;

namespace API.Models.Farmer
{
    public class ProcurementRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "CropListingId is required.")]
        public int CropListingId { get; set; }

        [ForeignKey("CropListingId")]
        public FarmerCropListing? CropListing { get; set; }

        [Required(ErrorMessage = "FarmerId is required.")]
        public int FarmerId { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        [Required(ErrorMessage = "AssignedFoId is required.")]
        public int AssignedFoId { get; set; }

        [ForeignKey("AssignedFoId")]
        public FieldOfficerProfile? AssignedFieldOfficer { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Requested quantity must be greater than 0.")]
        public decimal RequestedQuantity { get; set; }

        [Required(ErrorMessage = "Unit is required.")]
        [MaxLength(20, ErrorMessage = "Unit must not exceed 20 characters.")]
        [RegularExpression("^(kg|quintal|ton|bag)$",
            ErrorMessage = "Unit must be kg, quintal, ton, or bag.")]
        public string Unit { get; set; } = "";

        public bool? QualityPassed { get; set; }

        [MaxLength(1000, ErrorMessage = "FO notes must not exceed 1000 characters.")]
        public string? FoNotes { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters.")]
        [RegularExpression("^(Pending|InspectionScheduled|Approved|Rejected|Completed)$",
            ErrorMessage = "Status must be Pending, InspectionScheduled, Approved, Rejected, or Completed.")]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}