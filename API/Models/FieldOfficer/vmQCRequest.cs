using System;
using System.ComponentModel.DataAnnotations;

namespace API.Models.FieldOfficer
{
    public class vmQCRequest
    {
        [Required(ErrorMessage = "Farmer Name is required")]
        [StringLength(100, ErrorMessage = "Farmer Name cannot exceed 100 characters")]
        public string FarmerName { get; set; } = "";

        [Required(ErrorMessage = "Crop Type is required")]
        [StringLength(50)]
        public string CropType { get; set; } = "";

        [Required(ErrorMessage = "Quantity is required")]
        [Range(0.1, double.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Unit is required")]
        [StringLength(20)]
        public string Unit { get; set; } = "kg";

        [Required(ErrorMessage = "Location is required")]
        [StringLength(200)]
        public string Location { get; set; } = "";

        [Required(ErrorMessage = "Status is required")]
        [StringLength(30)]
        public string Status { get; set; } = "Pending";

        [Required]
        public int ProcurementRequestId { get; set; }

        [Required]
        public int? SlotId { get; set; }

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? SlotDate { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? StartTime { get; set; }

        [DataType(DataType.Time)]
        public TimeSpan? EndTime { get; set; }

        /// <summary>
        /// Farmer's original asking price per kg from crop listing
        /// Shown as reference in Quality Form
        /// </summary>
        [Display(Name = "Farmer Asking Price")]
        [Range(0, double.MaxValue)]
        public decimal AskingPrice { get; set; }
    }
}