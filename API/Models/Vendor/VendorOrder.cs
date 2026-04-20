using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Vendor
{
    public class VendorOrder
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "VendorId is required.")]
        public int VendorId { get; set; }

        [ForeignKey("VendorId")]
        public VendorProfile? Vendor { get; set; }

        [Required(ErrorMessage = "DeliveryLocationId is required.")]
        public int DeliveryLocationId { get; set; }

        [ForeignKey("DeliveryLocationId")]
        public VendorDeliveryLocation? DeliveryLocation { get; set; }

        public int? RepeatOrderSourceId { get; set; }

        [Required(ErrorMessage = "Delivery address is required.")]
        [MaxLength(500, ErrorMessage = "Delivery address must not exceed 500 characters.")]
        public string DeliveryAddress { get; set; } = "";

        [Required(ErrorMessage = "Status is required.")]
        [MaxLength(50, ErrorMessage = "Status must not exceed 50 characters.")]
        [RegularExpression("^(Pending|Confirmed|Dispatched|Delivered|Cancelled|placed|admin_confirmed|in_transit)$",
            ErrorMessage = "Status must be Pending, Confirmed, Dispatched, Delivered, or Cancelled.")]
        public string Status { get; set; } = "Pending";

        [Range(0.01, double.MaxValue, ErrorMessage = "Total amount must be greater than 0.")]
        public decimal TotalAmount { get; set; }

        public DateTime? PreferredDeliveryDate { get; set; }
        public DateTime? EstimatedDeliveryDate { get; set; }

        [MaxLength(100, ErrorMessage = "Logistics partner must not exceed 100 characters.")]
        public string? LogisticsPartner { get; set; }

        [MaxLength(100, ErrorMessage = "Tracking number must not exceed 100 characters.")]
        public string? TrackingNumber { get; set; }

        public DateTime? DispatchedAt { get; set; }
        public DateTime? ReceiptConfirmedAt { get; set; }

        [MaxLength(500, ErrorMessage = "Cancel reason must not exceed 500 characters.")]
        public string? CancelReason { get; set; }

        public bool IsRepeatOrder { get; set; } = false;

        public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}