using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Admin
{
    public class vm_AdminDashboard
    {
        
    }

    public class vm_KpiCount
    {
        public int Count { get; set; }
    }
 
    public class vm_ChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }
 
    public class vm_CropListing
    {
        public string FarmerName    { get; set; } = string.Empty;
        public string CropType      { get; set; } = string.Empty;
        public decimal Quantity     { get; set; }
        public string Unit     { get; set; }
        public string QualityStatus { get; set; } = string.Empty;
        public DateTime ListingTime { get; set; }
    }
 
    public class vm_PendingApproval
    {
        public int UserId       { get; set; }
        public string FullName  { get; set; } = string.Empty;
        public string Role      { get; set; } = string.Empty;
        public DateTime AppliedOn { get; set; }
    }
 
    public class vm_DashboardKpi
    {
        public decimal TodayRevenue  { get; set; }
        public int TotalFarmers      { get; set; }
        public int TotalVendors      { get; set; }
        public int ActiveFOs         { get; set; }
        public int TodayNewOrders    { get; set; }
    }
}