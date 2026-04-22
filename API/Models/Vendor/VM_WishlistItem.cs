namespace API.Models.Vendor
{
    public class VM_WishlistItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public string Category { get; set; } = "";
        public string Unit { get; set; } = "kg";
        public decimal AvgPrice { get; set; }
        public decimal QuantityAvailable { get; set; }
        public string? ImageUrl { get; set; }
        public string? Grade { get; set; }
    }
}