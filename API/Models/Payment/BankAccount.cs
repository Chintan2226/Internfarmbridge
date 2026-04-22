using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Auth;

namespace API.Models.Payment
{
    public class BankAccount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "UserId is required.")]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required(ErrorMessage = "Account holder name is required.")]
        [MaxLength(150, ErrorMessage = "Account holder name must not exceed 150 characters.")]
        [MinLength(2, ErrorMessage = "Account holder name must be at least 2 characters.")]
        public string AccountHolderName { get; set; } = "";

        [Required(ErrorMessage = "Account number is required.")]
        [MaxLength(20, ErrorMessage = "Account number must not exceed 20 characters.")]
        [RegularExpression(@"^\d{9,18}$", ErrorMessage = "Account number must be 9–18 digits.")]
        public string AccountNumber { get; set; } = "";

        [Required(ErrorMessage = "Bank name is required.")]
        [MaxLength(150, ErrorMessage = "Bank name must not exceed 150 characters.")]
        public string BankName { get; set; } = "";

        [MaxLength(150, ErrorMessage = "Branch name must not exceed 150 characters.")]
        public string? BranchName { get; set; }

        [Required(ErrorMessage = "Account type is required.")]
        [MaxLength(50, ErrorMessage = "Account type must not exceed 50 characters.")]
        [RegularExpression("^(Savings|Current|Overdraft)$",
            ErrorMessage = "Account type must be Savings, Current, or Overdraft.")]
        public string AccountType { get; set; } = "Savings";

        [Required(ErrorMessage = "IFSC code is required.")]
        [MaxLength(11, ErrorMessage = "IFSC code must not exceed 11 characters.")]
        [RegularExpression(@"^[A-Z]{4}0[A-Z0-9]{6}$", ErrorMessage = "Invalid IFSC code format.")]
        public string IfscCode { get; set; } = "";

        [MaxLength(50, ErrorMessage = "UPI ID must not exceed 50 characters.")]
        [RegularExpression(@"^[\w.\-]{2,256}@[a-zA-Z]{2,64}$", ErrorMessage = "Invalid UPI ID format.")]
        public string? UpiId { get; set; }

        [MaxLength(50, ErrorMessage = "Verification status must not exceed 50 characters.")]
        [RegularExpression("^(Pending|Verified|Failed)$",
            ErrorMessage = "Verification status must be Pending, Verified, or Failed.")]
        public string VerificationStatus { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}