using System;

namespace API.Models.Vendor
{
    public class VM_PaymentHistory
    {
        public string TransactionId { get; set; } = "";
        public string OrderId { get; set; } = "";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
        public string StatusColor { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
        public string GatewayRef { get; set; } = "";
        public DateTime PaymentDate { get; set; }
    }
}