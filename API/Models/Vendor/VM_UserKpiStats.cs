namespace API.Models.Vendor
{
    public class VM_UserKpiStats
    {
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public int PendingOrders { get; set; }
        public decimal TotalQuantity { get; set; }
    }
}