using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Farmer;
using API.Models.Admin;

namespace API.Models.FieldOfficer
{
    public class WarehouseLot
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "WarehouseId is required.")]
        public int WarehouseId { get; set; }

        [ForeignKey("WarehouseId")]
        public Warehouse? Warehouse { get; set; }

        [Required(ErrorMessage = "CatalogProductId is required.")]
        public int CatalogProductId { get; set; }

        [ForeignKey("CatalogProductId")]
        public CatalogProduct? CatalogProduct { get; set; }

        [Required(ErrorMessage = "FarmerId is required.")]
        public int FarmerId { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        public int? FoId { get; set; }

        [ForeignKey("FoId")]
        public FieldOfficerProfile? FieldOfficer { get; set; }

        public int? QualityInspectionId { get; set; }

        [ForeignKey("QualityInspectionId")]
        public QualityInspectionForm? QualityInspection { get; set; }

        [Required(ErrorMessage = "Grade is required.")]
        [MaxLength(10, ErrorMessage = "Grade must not exceed 10 characters.")]
        [RegularExpression("^(A|B|C|D)$", ErrorMessage = "Grade must be A, B, C, or D.")]
        public string Grade { get; set; } = "D";

        [MaxLength(100, ErrorMessage = "Variety must not exceed 100 characters.")]
        public string? Variety { get; set; }

        [MaxLength(20, ErrorMessage = "Unit must not exceed 20 characters.")]
        [RegularExpression("^(kg|quintal|ton|bag)$",
            ErrorMessage = "Unit must be kg, quintal, ton, or bag.")]
        public string? Unit { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Quantity accepted must be 0 or greater.")]
        public decimal QuantityAccepted { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Quantity remaining must be 0 or greater.")]
        public decimal QuantityRemaining { get; set; }

        [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters.")]
        [RegularExpression("^(Available|Reserved|Sold|Dispatched)$",
            ErrorMessage = "Status must be Available, Reserved, Sold, or Dispatched.")]
        public string Status { get; set; } = "Available";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}