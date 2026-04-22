using System;
using System.ComponentModel.DataAnnotations;

namespace API.Models.Auth
{
    public class Auth
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Name is required.")]
        public string? UserName { get; set; }

        [Required(ErrorMessage = "Email is Compalsory.")]
        [EmailAddress(ErrorMessage = "Formation of email is not matched.")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "Password is Mendatory.")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$", ErrorMessage = "Password Must contain 8 characaters, 1 Uppercase alphabet,1 Special Character and 1 Number.")]
        public string? Password { get; set; }

        public string? Mobile { get; set; }
        public string? Gender { get; set; }
        public string? Role { get; set; }
    }
}