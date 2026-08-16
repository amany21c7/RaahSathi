using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaahSathi.Models
{
    public class ReferralWithdrawalRequest
    {
        public int Id { get; set; }

        public int UserId { get; set; }

        [Required]
        [StringLength(20)]
        public string UserRole { get; set; } = "Customer"; // "Customer", "Mechanic"

        public double Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PayoutMethod { get; set; } = "UPI"; // "UPI", "Bank"

        [StringLength(200)]
        public string AccountHolderName { get; set; } = string.Empty;

        [StringLength(100)]
        public string BankAccountNumber { get; set; } = string.Empty;

        [StringLength(200)]
        public string BankName { get; set; } = string.Empty;

        [StringLength(50)]
        public string IfscCode { get; set; } = string.Empty;

        [StringLength(100)]
        public string UpiId { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAt { get; set; }

        [StringLength(500)]
        public string AdminRemarks { get; set; } = string.Empty;

        [StringLength(100)]
        public string TransactionReference { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}
