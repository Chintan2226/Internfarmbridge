using System;
using System.Collections.Generic;

namespace API.Models.Vendor
{
    public class VM_OrderHistoryItem
    {
        public string OrderId { get; set; } = "";
        public string OrderNumber { get; set; } = "";
        public DateTime PlacedAt { get; set; }
        public string Status { get; set; } = "";
        public string StatusText { get; set; } = "";
        public string StatusColor { get; set; } = "";
        public decimal GrandTotal { get; set; }
        public string TrackingId { get; set; } = "";
        public DateTime? EstimatedDelivery { get; set; }
        public int ItemCount { get; set; }
    }
}