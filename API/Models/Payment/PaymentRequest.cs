using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Farmer;
using API.Models.Admin;

namespace API.Models.Payment
{
    public class PaymentRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "FarmerId is required.")]
        public int FarmerId { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        [Required(ErrorMessage = "ProcurementRequestId is required.")]
        public int ProcurementRequestId { get; set; }

        [ForeignKey("ProcurementRequestId")]
        public ProcurementRequest? ProcurementRequest { get; set; }

        [Required(ErrorMessage = "AdminId is required.")]
        public int AdminId { get; set; }

        [ForeignKey("AdminId")]
        public AdminProfile? Admin { get; set; }

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters.")]
        [RegularExpression("^(Pending|Approved|Rejected|Disbursed)$",
            ErrorMessage = "Status must be Pending, Approved, Rejected, or Disbursed.")]
        public string Status { get; set; } = "Pending";

        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
    }
}