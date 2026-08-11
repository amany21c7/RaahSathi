using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class JobChatMessage
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int SenderId { get; set; }
        
        [Required]
        [StringLength(20)]
        public string SenderRole { get; set; } = "Customer"; // "Customer" or "Mechanic"
        
        [Required]
        [StringLength(100)]
        public string SenderName { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string MessageText { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
