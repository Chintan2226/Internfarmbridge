using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Vendor
{
    public class OrderTracking
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "OrderId is required.")]
        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public VendorOrder? Order { get; set; }

        [Required(ErrorMessage = "Status label is required.")]
        [MaxLength(100, ErrorMessage = "Status label must not exceed 100 characters.")]
        [RegularExpression("^(OrderPlaced|Confirmed|Packed|Dispatched|OutForDelivery|Delivered|Cancelled)$",
            ErrorMessage = "Status label must be a valid tracking status.")]
        public string StatusLabel { get; set; } = "";

        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    }
}