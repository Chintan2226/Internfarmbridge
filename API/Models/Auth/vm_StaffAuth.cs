using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace API.Models.Auth
{
    public class vm_StaffAuth
    {
        
    }

    public class vm_StaffLogin
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = string.Empty;
 
        [Required(ErrorMessage = "Password is required.")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;
 
        /// <summary>
        /// Optional hint from the UI tab selection.
        /// Values: "admin" | "field_officer"
        /// The API uses this only for a helpful mismatch error message.
        /// </summary>
    }
}
 
namespace Shared.Models
{
    // ─────────────────────────────────────────────────────────────
    // DOMAIN MODELS  (map to DB rows — no ORM)
    // ─────────────────────────────────────────────────────────────
 
    /// <summary>Maps to t_users</summary>
    public class User
    {
        public long   Id           { get; set; }
        public string Email        { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role         { get; set; } = string.Empty;   // "admin" | "field_officer" | "farmer" | "vendor"
        public bool   IsActive     { get; set; }
        public bool   IsApproved   { get; set; }
    }
 
    /// <summary>Minimal projection of t_field_officer_profiles needed for JWT claims</summary>
    public class FieldOfficerProfile
    {
        public long  Id             { get; set; }
        public long  UserId         { get; set; }
        public long? WarehouseId    { get; set; }
        public string? AssignedRegion { get; set; }
        public string? FullName      { get; set; }
    }
}