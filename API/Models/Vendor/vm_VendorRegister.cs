using System.ComponentModel.DataAnnotations;
namespace API.Models.Vendor
{
    public class vm_VendorRegister
    {
        [Required(ErrorMessage = "Business name is required.")]
        [MaxLength(200, ErrorMessage = "Business name must not exceed 200 characters.")]
        [MinLength(2, ErrorMessage = "Business name must be at least 2 characters.")]
        public string BusinessName { get; set; }

        [Required(ErrorMessage = "Contact person name is required.")]
        [MaxLength(150, ErrorMessage = "Contact person must not exceed 150 characters.")]
        [MinLength(2, ErrorMessage = "Contact person must be at least 2 characters.")]
        public string ContactPerson { get; set; }

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [MaxLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Mobile number is required.")]
        [RegularExpression(@"^\+?[0-9\s\-()]{7,15}$", ErrorMessage = "Invalid phone number format. Accepted formats: +919876543210, 9876543210")]
        [MaxLength(15, ErrorMessage = "Phone number must not exceed 15 characters.")]
        public string Phone { get; set; }

        [MaxLength(15, ErrorMessage = "GSTIN must not exceed 15 characters.")]
        [RegularExpression(@"^([0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1})?$",
            ErrorMessage = "Invalid GSTIN format. Format: XX XXXXX XXXX X XXX")]
        public string Gstin { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        [MaxLength(128, ErrorMessage = "Password must not exceed 128 characters.")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Password confirmation is required.")]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }
    }
}
