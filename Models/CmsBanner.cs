using System;

namespace RaahSathi.Models
{
    public class CmsBanner
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string TargetPage { get; set; } = "Homepage";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
