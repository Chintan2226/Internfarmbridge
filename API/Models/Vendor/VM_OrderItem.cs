using System.Collections.Generic;

namespace API.Models.Vendor
{
    public class VM_OrderItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public string Unit { get; set; } = "kg";
        public string FarmerName { get; set; } = "";
        public string Grade { get; set; }  // ← ADD THIS
        public decimal Total { get; set; }
    }

    public class VM_TimelineStep
    {
        public string StatusKey { get; set; } = "";
        public string StatusName { get; set; } = "";
        public string Icon { get; set; } = "";
        public bool IsCompleted { get; set; }
        public bool IsActive { get; set; }
        public string StatusDate { get; set; } = "";
    }

    public class VM_OrderTracking
    {
        public string OrderId { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public string TrackingId { get; set; } = "";
        public DateTime EstimatedDelivery { get; set; }
        public List<VM_TimelineStep> Timeline { get; set; } = new();
        public List<VM_OrderItem> Items { get; set; } = new();
    }
}