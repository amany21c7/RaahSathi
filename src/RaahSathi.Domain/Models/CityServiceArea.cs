using System;

namespace RaahSathi.Models
{
    public class CityServiceArea
    {
        public int Id { get; set; }
        public string State { get; set; } = "Uttar Pradesh";
        public string CityName { get; set; } = "Noida";
        public string AreaName { get; set; } = "Sector 62";
        public double ServiceRadiusKm { get; set; } = 15.0;
        public bool IsActive { get; set; } = true;
        public bool IsEmergencyMode { get; set; } = false;
        public string EmergencyReason { get; set; } = "Heavy Rain 🌧️";
    }
}
