using System;
using System.ComponentModel.DataAnnotations;

namespace API.Models
{
    public class vmProfile
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        [StringLength(100)]
        public string Email { get; set; }

        [Phone(ErrorMessage = "Invalid phone number")]
        [StringLength(15, ErrorMessage = "Phone number cannot exceed 15 characters")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Warehouse is required")]
        public int WarehouseId { get; set; }

        [StringLength(100, ErrorMessage = "Region cannot exceed 100 characters")]
        public string? AssignedRegion { get; set; }
        [StringLength(100, ErrorMessage = "Ware House cannot exceed 100 characters")]
        public string WarehouseName { get; set; }
        [StringLength(100, ErrorMessage = "Ware House Address cannot exceed 300 characters")]
        public string WarehouseAddress { get; set; }
        [Url(ErrorMessage = "Invalid image URL")]
        public string? ProfileImageUrl { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}