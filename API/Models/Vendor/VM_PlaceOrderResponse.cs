namespace API.Models.Vendor
{
    public class VM_PlaceOrderResponse
    {
        public bool Success { get; set; }
        public string OrderId { get; set; } = "";
        public string Message { get; set; } = "";
        public decimal Amount { get; set; }
        public string RazorpayOrderId { get; set; } = "";
    }
}