using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaahSathi.Models
{
    public class ReferralTransaction
    {
        public int Id { get; set; }

        public int ReferrerUserId { get; set; }

        public int RefereeUserId { get; set; }

        [Required]
        [StringLength(20)]
        public string StageType { get; set; } = "C2C"; // "M2M", "M2C", "C2C", "C2M"

        [StringLength(50)]
        public string ReferralCodeUsed { get; set; } = string.Empty;

        public double ReferrerRewardAmount { get; set; }

        public double RefereeRewardAmount { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending"; // "Pending", "Completed", "Cancelled"

        public int? TriggerJobId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        [StringLength(255)]
        public string Remarks { get; set; } = string.Empty;

        [ForeignKey("ReferrerUserId")]
        public virtual User? ReferrerUser { get; set; }

        [ForeignKey("RefereeUserId")]
        public virtual User? RefereeUser { get; set; }

        [ForeignKey("TriggerJobId")]
        public virtual Job? TriggerJob { get; set; }
    }
}
