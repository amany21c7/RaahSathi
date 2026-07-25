using System;

namespace RaahSathi.Models
{
    public class PushNotificationLog
    {
        public int Id { get; set; }
        public string TargetAudience { get; set; } = "All Users"; // Customer, Mechanics, Cities, All Users
        public string SelectedCity { get; set; } = "All";
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int SentCount { get; set; } = 1;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
