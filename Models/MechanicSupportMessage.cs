using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class MechanicSupportMessage
    {
        public int Id { get; set; }

        public int MechanicId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = "Support Notification";

        [Required]
        [StringLength(2000)]
        public string MessageText { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string SenderRole { get; set; } = "Admin"; // "Admin", "Support", "Mechanic"

        [Required]
        [StringLength(100)]
        public string SenderName { get; set; } = "RaahSathi Support Team";

        public bool IsFromAdmin { get; set; } = true;

        public bool IsRead { get; set; } = false;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
