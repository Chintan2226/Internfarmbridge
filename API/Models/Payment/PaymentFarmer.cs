using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Farmer;
using API.Models.FieldOfficer;

namespace API.Models.Payment
{
    public class PaymentFarmer
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "FarmerId is required.")]
        public int FarmerId { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        [Required(ErrorMessage = "PaymentRequestId is required.")]
        public int PaymentRequestId { get; set; }

        [ForeignKey("PaymentRequestId")]
        public PaymentRequest? PaymentRequest { get; set; }

        [Required(ErrorMessage = "ProcurementRequestId is required.")]
        public int ProcurementRequestId { get; set; }

        [ForeignKey("ProcurementRequestId")]
        public ProcurementRequest? ProcurementRequest { get; set; }

        [Required(ErrorMessage = "LotId is required.")]
        public int LotId { get; set; }

        [ForeignKey("LotId")]
        public WarehouseLot? Lot { get; set; }

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Payment number must be at least 1.")]
        public int PaymentNumber { get; set; }

        [MaxLength(100, ErrorMessage = "Trigger event must not exceed 100 characters.")]
        [RegularExpression("^(QualityApproved|LotDispatched|DeliveryConfirmed|Manual)$",
            ErrorMessage = "Trigger event must be QualityApproved, LotDispatched, DeliveryConfirmed, or Manual.")]
        public string? TriggerEvent { get; set; }

        [MaxLength(50, ErrorMessage = "Payment mode must not exceed 50 characters.")]
        [RegularExpression("^(NEFT|RTGS|IMPS|UPI)$",
            ErrorMessage = "Payment mode must be NEFT, RTGS, IMPS, or UPI.")]
        public string? PaymentMode { get; set; }

        [MaxLength(50, ErrorMessage = "UTR reference must not exceed 50 characters.")]
        public string? UtrReference { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters.")]
        [RegularExpression("^(Pending|Processing|Settled|Failed)$",
            ErrorMessage = "Status must be Pending, Processing, Settled, or Failed.")]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SettledAt { get; set; }
    }
}