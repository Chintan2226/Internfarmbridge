using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.FieldOfficer
{
    /// <summary>
    /// Model for quality inspection form submitted by Field Officer.
    /// Maps to t_quality_inspection_forms table.
    /// </summary>
    public class QualityInspectionForm
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "ProcurementRequestId is required.")]
        public int ProcurementRequestId { get; set; }

        [Required(ErrorMessage = "FoId is required.")]
        public int FoId { get; set; }

        [Range(0, 100, ErrorMessage = "Moisture percentage must be between 0 and 100.")]
        public decimal MoisturePct { get; set; }

        [Range(0, 100, ErrorMessage = "Foreign matter percentage must be between 0 and 100.")]
        public decimal ForeignMatterPct { get; set; }

        [MaxLength(500, ErrorMessage = "Pest/disease observation must not exceed 500 characters.")]
        public string? PestDiseaseObserved { get; set; }

        [MaxLength(100, ErrorMessage = "Variety must not exceed 100 characters.")]
        public string? Variety { get; set; }

        [MaxLength(10, ErrorMessage = "Grade must not exceed 10 characters.")]
        [RegularExpression("^(A|B|C|D)$", ErrorMessage = "Grade must be A, B, C, or D.")]
        public string? Grade { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Weight checked must be greater than 0.")]
        public decimal WeightCheckedKg { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Accepted quantity must be 0 or greater.")]
        public decimal AcceptedQuantity { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Rejected quantity must be 0 or greater.")]
        public decimal RejectedQuantity { get; set; }

        [MaxLength(1000, ErrorMessage = "Defects noted must not exceed 1000 characters.")]
        public string? DefectsNoted { get; set; }

        [MaxLength(1000, ErrorMessage = "Remarks must not exceed 1000 characters.")]
        public string? Remarks { get; set; }

        [Required(ErrorMessage = "Passed status is required.")]
        public bool Passed { get; set; }

        /// <summary>
        /// Price per kg assessed by Field Officer during inspection.
        /// If not provided (0), falls back to farmer's asking price.
        /// Used for 30% advance payment calculation.
        /// Formula: TotalValue = AcceptedQuantity × (FoAssessedPrice or FarmerAskingPrice)
        /// </summary>
        [Range(0, double.MaxValue, ErrorMessage = "Assessed price must be 0 or greater.")]
        [Display(Name = "FO Assessed Price (₹/kg)")]
        public decimal FoAssessedPrice { get; set; }

        /// <summary>
        /// Calculated automatically — not sent from frontend
        /// </summary>
        public decimal TotalValue      => Math.Round(AcceptedQuantity * FoAssessedPrice, 2);
        public decimal AdvanceAmount   => Math.Round(TotalValue * 0.30m, 2);
        public decimal RemainingAmount => Math.Round(TotalValue * 0.70m, 2);

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}