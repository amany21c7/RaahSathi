using System;

namespace RaahSathi.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string AdminName { get; set; } = "Super Admin";
        public string ActionType { get; set; } = "UPDATE";
        public string Details { get; set; } = string.Empty;
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        public string IpAddress { get; set; } = "127.0.0.1";
        public string UserAgent { get; set; } = "Chrome Browser";
    }
}
