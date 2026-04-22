using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Farmer
{
    public class ListingPhoto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "ListingId is required.")]
        public int ListingId { get; set; }

        [ForeignKey("ListingId")]
        public FarmerCropListing? Listing { get; set; }

        [Required(ErrorMessage = "Photo URL is required.")]
        [Url(ErrorMessage = "Photo URL must be a valid URL.")]
        [MaxLength(2048, ErrorMessage = "Photo URL must not exceed 2048 characters.")]
        public string PhotoUrl { get; set; } = "";
    }
}