using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Auth
{
    public class vm_AuthFarmer
    {
        
    }

 
    // ─── ViewModels ─────────────────────────────────────────────
 
    /// Used in POST /api/farmer/register
    public class vm_FarmerRegister
    {
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
        public string? Address { get; set; }
        public string? State { get; set; }
        public string? District { get; set; }
    }
 
    /// Used in POST /api/farmer/login
    public class vm_FarmerLogin
    {
        public string EmailOrPhone { get; set; } = "";
        public string Password { get; set; } = "";
    }
 
    /// Used in POST /api/farmer/send-otp
    public class vm_FarmerOtpRequest
    {
        public string Email { get; set; } = "";
    }
 
    /// Used in POST /api/farmer/reset-password
    public class vm_FarmerResetPassword
    {
        public string Email { get; set; } = "";
        public string Otp { get; set; } = "";
        public string NewPassword { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
    }
}