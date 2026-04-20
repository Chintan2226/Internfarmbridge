using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Admin
{
    public class Warehouse
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Warehouse name is required.")]
        [MaxLength(200, ErrorMessage = "Warehouse name must not exceed 200 characters.")]
        [MinLength(2, ErrorMessage = "Warehouse name must be at least 2 characters.")]
        public string Name { get; set; } = "";

        [MaxLength(500, ErrorMessage = "Address must not exceed 500 characters.")]
        public string? Address { get; set; }

        [MaxLength(100, ErrorMessage = "State must not exceed 100 characters.")]
        public string? State { get; set; }

        [MaxLength(100, ErrorMessage = "District must not exceed 100 characters.")]
        public string? District { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Daily capacity must be at least 1.")]
        public int DailyCapacity { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // KPI metrics
        public decimal CapacityMt { get; set; }
        public decimal UsedMt { get; set; }
        public decimal AvailableMt { get; set; }
        public decimal UtilizationPercentage { get; set; }
        public string Status { get; set; } = "Normal";
    }
}