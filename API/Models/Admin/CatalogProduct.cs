using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Admin
{
    public class CatalogProduct
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "Product name is required.")]
        [MaxLength(200, ErrorMessage = "Product name must not exceed 200 characters.")]
        [MinLength(2, ErrorMessage = "Product name must be at least 2 characters.")]
        public string Name { get; set; } = "";

        [MaxLength(100, ErrorMessage = "Category must not exceed 100 characters.")]
        public string Category { get; set; } = "";

        [Required(ErrorMessage = "Unit of measure is required.")]
        [MaxLength(20, ErrorMessage = "Unit of measure must not exceed 20 characters.")]
        public string UnitOfMeasure { get; set; } = "";

        [MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters.")]
        public string? Description { get; set; }

        [Url(ErrorMessage = "Image URL must be a valid URL.")]
        [MaxLength(2048, ErrorMessage = "Image URL must not exceed 2048 characters.")]
        public string? ImageUrl { get; set; }

        // JSON stored as string — validated at application layer
        [MaxLength(4000, ErrorMessage = "Quality parameters must not exceed 4000 characters.")]
        public string? QualityParameters { get; set; }

        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "CreatedBy (AdminId) is required.")]
        public int CreatedBy { get; set; }

        [ForeignKey("CreatedBy")]
        public AdminProfile? Admin { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}