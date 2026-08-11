using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class PricingRule
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string VehicleCategory { get; set; } = string.Empty; // "2-Wheeler", "Car", "Commercial", "Heavy"

        [StringLength(100)]
        public string CityName { get; set; } = "All Cities"; // "All Cities", "Noida", "Delhi", "Lucknow", "Mumbai", etc.

        public double BaseFee { get; set; }
        public double PerKmRate { get; set; }

        public double BaseTowingFee { get; set; }
        public double PerKmTowingRate { get; set; }
    }
}
