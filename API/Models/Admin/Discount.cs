using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Admin
{
    public class Discount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "CatalogProductId is required.")]
        public int CatalogProductId { get; set; }

        [ForeignKey("CatalogProductId")]
        public CatalogProduct? CatalogProduct { get; set; }

        [Required(ErrorMessage = "Discount percentage is required.")]
        [Range(0.01, 100, ErrorMessage = "Discount percentage must be between 0.01 and 100.")]
        public decimal DiscountPct { get; set; }

        [Required(ErrorMessage = "Source is required.")]
        [MaxLength(50, ErrorMessage = "Source must not exceed 50 characters.")]
        [RegularExpression("^(manual|ai_generated)$",
            ErrorMessage = "Source must be 'manual' or 'ai_generated'.")]
        public string Source { get; set; } = "manual";

        public bool RuleIsActive { get; set; } = true;

        public DateTime? ValidFrom { get; set; }
        public DateTime? ValidTo { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}