using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Payment
{
    public class vm_PendingPayment
    {
        public int PaymentId { get; set; }
        public string CropName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal AdvancePaid { get; set; }
        public decimal BalancePending { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}