using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.DTOs
{
    public class UserProfileDto
    {
        public int Id { get; set; }
        public string DisplayId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        // Mechanic specific info if role == Mechanic
        public MechanicProfileDto? MechanicProfile { get; set; }
    }

    public class MechanicProfileDto
    {
        public bool IsOnline { get; set; }
        public double Rating { get; set; }
        public int TotalJobs { get; set; }
        public string KycStatus { get; set; } = string.Empty;
        public string ShopName { get; set; } = string.Empty;
        public string ShopAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public double CurrentEarnings { get; set; }
        public string VehicleExpertise { get; set; } = string.Empty;
        public string Specialization { get; set; } = string.Empty;
        public int ServiceRadiusKm { get; set; }
        public string Languages { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string BankAccountNumber { get; set; } = string.Empty;
        public string IfscCode { get; set; } = string.Empty;
        public string UpiId { get; set; } = string.Empty;
        public string AccountHolderName { get; set; } = string.Empty;
    }

    public class UpdateProfileRequestDto
    {
        [Required]
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? City { get; set; }

        // Mechanic updates
        public string? ShopName { get; set; }
        public string? ShopAddress { get; set; }
        public string? VehicleExpertise { get; set; }
        public string? Specialization { get; set; }
        public string? BankName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? IfscCode { get; set; }
        public string? UpiId { get; set; }
        public string? AccountHolderName { get; set; }
    }
}
