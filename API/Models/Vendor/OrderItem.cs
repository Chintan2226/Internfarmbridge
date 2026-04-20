using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Farmer;
using API.Models.FieldOfficer;
using API.Models.Admin;

namespace API.Models.Vendor
{
    public class OrderItem
    {
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required(ErrorMessage = "OrderId is required.")]
    public int OrderId { get; set; }

    [ForeignKey("OrderId")]
    public VendorOrder Order { get; set; }

    [Required(ErrorMessage = "CatalogProductId is required.")]
    public int CatalogProductId { get; set; }

    [ForeignKey("CatalogProductId")]
    public CatalogProduct CatalogProduct { get; set; }

    public int? CropListingId { get; set; }

    [ForeignKey("CropListingId")]
    public FarmerCropListing CropListing { get; set; }

    [Required(ErrorMessage = "LotId is required.")]
    public int LotId { get; set; }

    [ForeignKey("LotId")]
    public WarehouseLot Lot { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Quantity must be greater than 0.")]
    public decimal Quantity { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0.")]
    public decimal UnitPrice { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Subtotal must be greater than 0.")]
    public decimal Subtotal { get; set; }
}
}