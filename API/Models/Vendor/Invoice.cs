using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API.Models.Vendor
{
    public class Invoice
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "OrderId is required.")]
        public int OrderId { get; set; }

        [ForeignKey("OrderId")]
        public VendorOrder? Order { get; set; }

        [Required(ErrorMessage = "Invoice number is required.")]
        [MaxLength(50, ErrorMessage = "Invoice number must not exceed 50 characters.")]
        public string InvoiceNumber { get; set; } = "";

        [Url(ErrorMessage = "Invoice URL must be a valid URL.")]
        [MaxLength(2048, ErrorMessage = "Invoice URL must not exceed 2048 characters.")]
        public string? InvoiceUrl { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Subtotal must be 0 or greater.")]
        public decimal Subtotal { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Platform fee must be 0 or greater.")]
        public decimal PlatformFee { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Transport fee must be 0 or greater.")]
        public decimal TransportFee { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Tax amount must be 0 or greater.")]
        public decimal TaxAmount { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Discount applied must be 0 or greater.")]
        public decimal DiscountApplied { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Total amount must be greater than 0.")]
        public decimal TotalAmount { get; set; }

        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    }
}