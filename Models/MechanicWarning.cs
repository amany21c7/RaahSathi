using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class MechanicWarning
    {
        public int Id { get; set; }

        public int MechanicId { get; set; }
        public int? ComplaintId { get; set; }

        [StringLength(100)]
        public string WarningType { get; set; } = "Official Warning"; // "Notice", "Official Warning", "Final Warning"

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty; // Admin warning message text

        public bool IsAcknowledged { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User? Mechanic { get; set; }
        public MechanicComplaint? Complaint { get; set; }
    }
}
