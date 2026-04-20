namespace API.Models.Vendor
{
    public class VM_RazorpayOrderRequest
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
    }

    public class VM_RazorpayOrderResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string OrderId { get; set; } = "";
        public string RazorpayOrderId { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public string KeyId { get; set; } = "";
    }

    public class VM_RazorpayPaymentVerification
    {
        public int OrderId { get; set; }
        public string RazorpayOrderId { get; set; } = "";
        public string RazorpayPaymentId { get; set; } = "";
        public string RazorpaySignature { get; set; } = "";
    }

    public class VM_RazorpayPaymentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int OrderId { get; set; }
        public string PaymentId { get; set; } = "";
    }
}