using System.Collections.Generic;

namespace API.Models.Vendor
{
    public class VM_PlaceOrderRequest
    {
        public int AddressId { get; set; }
        public string PaymentMethod { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public List<VM_PlaceOrderItem> Items { get; set; } = new();
    }

    public class VM_PlaceOrderItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}