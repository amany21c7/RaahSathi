using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class MechanicComplaint
    {
        public int Id { get; set; }

        public int JobId { get; set; }
        public int CustomerId { get; set; }
        public int MechanicId { get; set; }

        public double Rating { get; set; } // 1.0, 2.0, 3.0

        [StringLength(500)]
        public string SelectedReasons { get; set; } = string.Empty; // Comma-separated complaint tags

        [StringLength(100)]
        public string Category { get; set; } = "General"; // "Service Quality", "Behaviour", "Safety", "Other"

        [StringLength(1000)]
        public string CustomerDetails { get; set; } = string.Empty; // Custom text complaint details

        [StringLength(50)]
        public string Status { get; set; } = "Pending"; // "Pending", "WarningSent", "Dismissed"

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Job? Job { get; set; }
        public User? Customer { get; set; }
        public User? Mechanic { get; set; }
    }
}
