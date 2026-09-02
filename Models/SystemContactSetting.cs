using System;

namespace RaahSathi.Models
{
    public class SystemContactSetting
    {
        public int Id { get; set; }
        public string HelplineNumber { get; set; } = "+91 9891819236";
        public string TollFreeNumber { get; set; } = "1800-102-7224";
        public string EmergencySupportNumber { get; set; } = "+91 9536838103";
        public string WhatsAppNumber { get; set; } = "+91 9891819236";
        public string SupportEmail { get; set; } = "support.raahsathi@gmail.com";
        public string BillingEmail { get; set; } = "billing@raahsathi.in";
        public string PartnerHelplineNumber { get; set; } = "+91 9891819236";
        public string OfficeAddress { get; set; } = "Tower B, DLF Cyber City, Sector 24, Gurugram, Haryana - 122002";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
