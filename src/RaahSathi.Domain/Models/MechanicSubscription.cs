using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class MechanicSubscription
    {
        public int Id { get; set; }

        [Required]
        public int MechanicId { get; set; }

        public double Amount { get; set; } = 0.0;

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime EndDate { get; set; } = DateTime.UtcNow.AddDays(30);

        [Required]
        [StringLength(50)]
        public string PaymentStatus { get; set; } = "Success"; // "Success", "ManualGrant", "Pending", "Failed"

        [StringLength(100)]
        public string? RazorpayPaymentId { get; set; }

        [StringLength(100)]
        public string? RazorpayOrderId { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User? Mechanic { get; set; }
    }
}
