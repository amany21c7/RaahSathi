using System;

namespace RaahSathi.Models
{
    public class AdminSystemSetting
    {
        public int Id { get; set; }
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public string Description { get; set; } = string.Empty;
    }
}
