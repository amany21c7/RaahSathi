using System;

namespace RaahSathi.Models
{
    public class CustomService
    {
        public int Id { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string IconClass { get; set; } = "fa-screwdriver-wrench";
        public string Category { get; set; } = "Breakdown";
        public double BasePrice { get; set; } = 199.0;
        public double MaxPrice { get; set; } = 499.0;
        public string Description { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }
}
