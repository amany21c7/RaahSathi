using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class Vehicle
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string VehicleType { get; set; } = "Car"; // "2-Wheeler", "Car", "Commercial", "Heavy"

        [Required]
        [StringLength(100)]
        public string Model { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string RegistrationNumber { get; set; } = string.Empty;

        [StringLength(500)]
        public string VehiclePhotoUrl { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User? User { get; set; }
    }
}
