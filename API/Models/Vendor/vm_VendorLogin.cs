using System.ComponentModel.DataAnnotations;

// This file is newly added and not originally part of project structure

namespace API.Models.Vendor
{
    public class vm_VendorLogin
    {
        [Required(ErrorMessage = "Email or mobile number is required.")]
        public string EmailOrPhone { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; }
    }
}
