using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaahSathi.Models
{
    public class MechanicPayoutRequest
    {
        public int Id { get; set; }

        public int MechanicId { get; set; }

        public double Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PayoutMethod { get; set; } = "Bank"; // "Bank" or "UPI"

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

        [ForeignKey("MechanicId")]
        public virtual User? Mechanic { get; set; }
    }

    public class PayoutRequestViewModel
    {
        public MechanicPayoutRequest Request { get; set; } = null!;
        public string MechanicName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string DisplayId { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    public class MechanicLedgerViewModel
    {
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string DisplayId { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public double CurrentEarnings { get; set; }
        public int TotalJobs { get; set; }
        public double Rating { get; set; }
        public string PreferredPayoutMethod { get; set; } = "UPI";
        public string BankName { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string IfscCode { get; set; } = string.Empty;
        public string UpiId { get; set; } = string.Empty;
        public string AccountHolderName { get; set; } = string.Empty;
        public double PendingPayoutAmount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
