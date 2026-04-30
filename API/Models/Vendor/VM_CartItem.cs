using System.Collections.Generic;

namespace API.Models.Vendor
{
    public class VM_CartItem
    {
        public int CartId { get; set; }
        public int CropId { get; set; }
        public string CropName { get; set; } = "";
        public string Unit { get; set; } = "kg";
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal AvailableStock { get; set; } 
        public bool IsStockValid { get; set; } = true;
        public string Grade {get;set;}
    }

    public class VM_CartSummary
    {
        public List<VM_CartItem> Items { get; set; } = new();
        public decimal TotalQuantity { get; set; }
        public decimal EstimatedOrderValue { get; set; }

        public bool HasStockIssues { get; set; } = false; 
    }
}