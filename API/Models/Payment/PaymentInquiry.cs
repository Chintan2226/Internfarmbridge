using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Farmer;

namespace API.Models.Payment
{
    public class PaymentInquiry
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "FarmerId is required.")]
        public int FarmerId { get; set; }

        [ForeignKey("FarmerId")]
        public FarmerProfile? Farmer { get; set; }

        [Required(ErrorMessage = "PaymentId is required.")]
        public int PaymentId { get; set; }

        [ForeignKey("PaymentId")]
        public PaymentFarmer? Payment { get; set; }

        [Required(ErrorMessage = "Message is required.")]
        [MaxLength(2000, ErrorMessage = "Message must not exceed 2000 characters.")]
        [MinLength(10, ErrorMessage = "Message must be at least 10 characters.")]
        public string Message { get; set; } = "";

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters.")]
        [RegularExpression("^(Open|InProgress|Resolved|Closed)$",
            ErrorMessage = "Status must be Open, InProgress, Resolved, or Closed.")]
        public string Status { get; set; } = "Open";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}