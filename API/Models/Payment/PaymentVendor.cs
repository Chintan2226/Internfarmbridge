using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Vendor;

namespace API.Models.Payment
{
    public class PaymentVendor
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "OrderId is required.")]
        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public VendorOrder? Order { get; set; }

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters.")]
        [RegularExpression("^(Pending|Completed|Failed|Refunded|initiated|refund_initiated)$",
            ErrorMessage = "Status must be Pending, Completed, Failed, or Refunded.")]
        public string Status { get; set; } = "Pending";

        [Required(ErrorMessage = "Payment method is required.")]
        [MaxLength(50, ErrorMessage = "Payment method must not exceed 50 characters.")]
        [RegularExpression("^(UPI|NetBanking|Card|Wallet|COD|upi|online_banking)$",
            ErrorMessage = "Payment method must be UPI, NetBanking, Card, Wallet, or COD.")]
        public string PaymentMethod { get; set; } = "UPI";

        [MaxLength(100, ErrorMessage = "Gateway reference must not exceed 100 characters.")]
        public string? GatewayRef { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}