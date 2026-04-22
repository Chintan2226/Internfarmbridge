using System;

namespace API.Models.Vendor
{
    public class VM_RecentOrder
    {
        public string OrderNumber { get; set; } = "";
        public DateTime PlacedAt { get; set; }
        public string Status { get; set; } = "";
        public string StatusText { get; set; } = "";
        public decimal GrandTotal { get; set; }
    }
}