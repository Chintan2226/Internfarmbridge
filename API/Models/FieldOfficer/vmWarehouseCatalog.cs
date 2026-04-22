using System;
using System.ComponentModel.DataAnnotations;

namespace API.Models.FieldOfficer
{
    public class vmWarehouseCatalog
    {
        [Required]
        public int LotId { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Product Name")]
        public string? ProductName { get; set; }

        [Required]
        [StringLength(50)]
        public string? Grade { get; set; }

        [StringLength(50)]
        public string? Variety { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal Quantity { get; set; }

        [Required]
        [StringLength(20)]
        public string? Unit { get; set; }

        [Required]
        [StringLength(100)]
        public string? Warehouse { get; set; }

        [Required]
        [StringLength(50)]
        public string? Status { get; set; }
    }
}