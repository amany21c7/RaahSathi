using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class ProblemTypePricing
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string ProblemName { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string VehicleCategory { get; set; } = "Car"; // "Car", "2-Wheeler", "Commercial", "Heavy", "All"

        [StringLength(100)]
        public string CityName { get; set; } = "All Cities"; // "All Cities", "Noida", "Delhi", "Lucknow", "Mumbai", etc.

        public double MinServiceCharge { get; set; }
        public double MaxServiceCharge { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
