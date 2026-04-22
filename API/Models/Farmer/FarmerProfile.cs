using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using API.Models.Auth;

namespace API.Models.Farmer
{
    public class FarmerProfile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(150)]
        public string FullName { get; set; } = "";

        [Required]
        [MaxLength(15)]
        public string Phone { get; set; } = "";

        [MaxLength(500)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? State { get; set; }

        [MaxLength(100)]
        public string? District { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}