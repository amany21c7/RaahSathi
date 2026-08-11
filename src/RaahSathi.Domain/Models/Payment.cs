using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class Payment
    {
        public int Id { get; set; }

        [Required]
        public int JobId { get; set; }

        public double Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentStatus { get; set; } = "Held"; // "Held" (Escrowed), "Released" (to Mechanic), "Refunded" (to Customer)

        [Required]
        [StringLength(100)]
        public string RazorpayPaymentId { get; set; } = string.Empty;

        // Tiered Commission Breakdown (8% < ₹1000, 10% >= ₹1000)
        public double AdminCommissionAmount { get; set; } = 0.0;
        public double MechanicEarningAmount { get; set; } = 0.0;
        public double CommissionRateUsed { get; set; } = 0.08;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public Job? Job { get; set; }
    }
}
