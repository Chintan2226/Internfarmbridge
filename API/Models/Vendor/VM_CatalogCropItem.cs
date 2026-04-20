namespace API.Models.Vendor
{
    public class VM_CatalogCropItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Unit { get; set; } = "kg";
        public decimal UnitPrice { get; set; }
        public decimal QuantityAvailable { get; set; }
        public string? ImageUrl { get; set; }
        public string Grade { get; set; }
    }
}