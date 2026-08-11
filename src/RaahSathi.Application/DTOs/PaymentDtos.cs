using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.DTOs
{
    public class ProcessPaymentRequestDto
    {
        [Required]
        public int JobId { get; set; }
        public string? PaymentId { get; set; }
        public string PaymentMethod { get; set; } = "UPI"; // "Razorpay", "UPI", "Cash", "Wallet"
        public double Amount { get; set; }
    }

    public class PaymentResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int JobId { get; set; }
        public string PaymentId { get; set; } = string.Empty;
        public double TotalAmount { get; set; }
        public double AdminCommission { get; set; }
        public double MechanicEarnings { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }
}
