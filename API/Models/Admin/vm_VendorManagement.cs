using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Admin
{
    public class vm_VendorManagement
    {
        
    }

    public class DemandTrendPoint
    {
        public string TimeLabel { get; set; } = string.Empty;
        public decimal TotalVolume { get; set; }
    }

    public class VendorRow
    {
        public string Id { get; set; } = string.Empty;
        public string BusinessName { get; set; } = string.Empty;
        public string ContactName { get; set; } = string.Empty;
        public string GstNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal TotalOrderValue { get; set; }
        public DateTime RegistrationDate { get; set; }
    }

    public class VendorReviewRow
    {
        public string ReviewId { get; set; } = string.Empty;
        public string VendorName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string ReviewText { get; set; } = string.Empty;
        public bool IsApproved { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty; 
    }

    public class VendorAnalyticsKpi
    {
        public int TotalVendors { get; set; }
        public decimal TotalOrdersValue { get; set; }
        public int OrdersPlaced { get; set; }
        public decimal FulfillmentRate { get; set; } // Returns a percentage like 94.0
    }

     public class AdminActionRequest
    {
        [Required]
        public string AdminId { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }
}