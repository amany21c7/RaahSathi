using System;

namespace RaahSathi.Models
{
    public class SystemApiSetting
    {
        public int Id { get; set; }
        public string SmsApiKey { get; set; } = string.Empty;
        public string WhatsAppBusinessNumber { get; set; } = string.Empty;
        public string GoogleMapsApiKey { get; set; } = string.Empty;
        public string SmtpSenderEmail { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
