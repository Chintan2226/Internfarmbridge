namespace API.Models.Vendor
{
    public class VM_DashboardStats
    {
        public int ActiveOrders { get; set; }
        public int TotalOrdersThisMonth { get; set; }
        public int CatalogCropsAvailable { get; set; }
        public decimal TotalQuantity { get; set; }
    }
}