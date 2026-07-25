using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class AdminWithdrawal
    {
        public int Id { get; set; }

        public double Amount { get; set; }

        [StringLength(100)]
        public string PayoutMethod { get; set; } = "Bank Transfer";

        [StringLength(100)]
        public string ReferenceNumber { get; set; } = string.Empty;

        public DateTime WithdrawnAt { get; set; } = DateTime.UtcNow;
    }
}
