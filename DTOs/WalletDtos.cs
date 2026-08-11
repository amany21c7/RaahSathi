using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.DTOs
{
    public class WalletBalanceDto
    {
        public int MechanicId { get; set; }
        public double CurrentBalance { get; set; }
        public double PendingPayoutAmount { get; set; }
        public double LifetimeEarnings { get; set; }
        public int TotalJobsCompleted { get; set; }
    }

    public class CreatePayoutRequestDto
    {
        [Required]
        public int MechanicId { get; set; }

        [Required]
        [Range(100, 100000, ErrorMessage = "Payout amount must be between 100 and 100,000")]
        public double Amount { get; set; }

        public string PayoutMethod { get; set; } = "UPI"; // "UPI" or "Bank"
        public string AccountHolderName { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string IfscCode { get; set; } = string.Empty;
        public string UpiId { get; set; } = string.Empty;
    }

    public class PayoutResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? PayoutRequestId { get; set; }
        public double RemainingBalance { get; set; }
    }

    public class AdminProcessPayoutDto
    {
        [Required]
        public int PayoutRequestId { get; set; }

        [Required]
        public string Action { get; set; } = "Approve"; // "Approve" or "Reject"

        public string Remarks { get; set; } = string.Empty;
        public string TransactionReference { get; set; } = string.Empty;
    }
}
