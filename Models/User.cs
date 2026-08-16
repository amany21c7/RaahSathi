using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaahSathi.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Role { get; set; } = "Customer"; // "Customer", "Mechanic", "Admin"

        [Required]
        [StringLength(100)]
        public string Password { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsBlocked { get; set; } = false;

        public string AdminRole { get; set; } = "Super Admin"; // "Super Admin", "Finance", "Support", "Operations", "Marketing", "Moderator"

        [StringLength(50)]
        public string ReferralCode { get; set; } = string.Empty;

        [StringLength(50)]
        public string? ReferredByCode { get; set; }

        public double ReferralWalletBalance { get; set; } = 0.0;

        // Custom Formatted Unique ID (e.g., RS01C for Customer, RS01M for Mechanic, RS01A for Admin)
        [NotMapped]
        public string DisplayId => Role == "Mechanic" 
            ? $"RS{Id:D2}M" 
            : Role == "Admin" 
                ? $"RS{Id:D2}A" 
                : $"RS{Id:D2}C";
    }
}
