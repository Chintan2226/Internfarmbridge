using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Payment
{
    public class vm_PaymentApproveRequest
    {
        public int PaymentId { get; set; }
        public int AdminId { get; set; }
        public string UtrReference { get; set; } = string.Empty;

        public int FarmerId { get; set; }  // ← ADD THIS LINE
        public decimal Amount { get; set; }      // ← ADD THIS
    }
}