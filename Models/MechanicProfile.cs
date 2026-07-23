using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class MechanicProfile
    {
        [Key]
        public int UserId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string DisplayId => $"RS{UserId:D2}M";

        public bool IsOnline { get; set; } = false;

        public double Latitude { get; set; } // Simulated live latitude
        public double Longitude { get; set; } // Simulated live longitude

        public double Rating { get; set; } = 5.0;
        public int TotalJobs { get; set; } = 0;

        [Required]
        [StringLength(20)]
        public string KycStatus { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"

        [StringLength(50)]
        public string AadhaarNumber { get; set; } = string.Empty;

        // Personal Information
        [StringLength(100)]
        public string Email { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        [StringLength(20)]
        public string Gender { get; set; } = string.Empty;
        [StringLength(500)]
        public string ProfilePhotoUrl { get; set; } = string.Empty;

        // Identity Verification (KYC)
        [StringLength(500)]
        public string AadhaarFrontUrl { get; set; } = string.Empty;
        [StringLength(500)]
        public string AadhaarBackUrl { get; set; } = string.Empty;
        [StringLength(500)]
        public string PanCardUrl { get; set; } = string.Empty;
        [StringLength(500)]
        public string SelfieUrl { get; set; } = string.Empty;

        // Shop Information
        [StringLength(200)]
        public string ShopName { get; set; } = string.Empty;
        [StringLength(500)]
        public string ShopPhotoUrl { get; set; } = string.Empty;
        [StringLength(500)]
        public string ShopAddress { get; set; } = string.Empty;
        [StringLength(10)]
        public string Pincode { get; set; } = string.Empty;
        [StringLength(100)]
        public string ShopTiming { get; set; } = string.Empty;

        // Experience & Certification
        public bool IsCertified { get; set; } = false;
        [StringLength(200)]
        public string GarageName { get; set; } = string.Empty;

        // Expertise
        [StringLength(1000)]
        public string VehicleExpertise { get; set; } = string.Empty; // Comma-separated: Bike, Scooter, Car...
        [StringLength(1000)]
        public string Specialization { get; set; } = string.Empty; // Comma-separated: Engine, Electrical, AC...
        public int ServiceRadiusKm { get; set; } = 10;

        [StringLength(200)]
        public string SkillCategory { get; set; } = "Car"; // e.g. "2-Wheeler, Car"

        public int ExperienceYears { get; set; } = 1;

        public double CommissionRate { get; set; } = 0.20; // Starts at 20% commission

        public double CurrentEarnings { get; set; } = 0.0; // Simulated wallet balance

        // Additional Profile Info & Payments (All Optional)
        [StringLength(200)]
        public string Languages { get; set; } = "Hindi, English";
        [StringLength(100)]
        public string WorkingHours { get; set; } = "9:00 AM - 9:00 PM";
        [StringLength(100)]
        public string BankName { get; set; } = string.Empty;
        [StringLength(50)]
        public string BankAccountNumber { get; set; } = string.Empty;
        [StringLength(20)]
        public string IfscCode { get; set; } = string.Empty;
        [StringLength(100)]
        public string UpiId { get; set; } = string.Empty;
        [StringLength(50)]
        public string PreferredPayoutMethod { get; set; } = "UPI"; // "UPI", "Bank", "Cash"
        public bool AcceptsCash { get; set; } = true;

        // Advanced Trust & Performance Metrics
        public int TotalReviewsCount { get; set; } = 0;
        public int RecommendedCount { get; set; } = 0;
        public int RecommendationPercentage { get; set; } = 98;
        public int SuccessRatePercentage { get; set; } = 95;
        public int AvgArrivalTimeMins { get; set; } = 18;
        public int AcceptanceRatePercentage { get; set; } = 96;
        public int CancellationRatePercentage { get; set; } = 1;
        public int RepeatCustomersCount { get; set; } = 14;

        // Dynamic Badge System
        public System.Collections.Generic.List<MechanicBadge> GetBadges()
        {
            var list = new System.Collections.Generic.List<MechanicBadge>();

            if (Rating >= 4.8)
            {
                list.Add(new MechanicBadge("🥇 Top Rated", "bg-warning text-dark", "fa-solid fa-trophy"));
                list.Add(new MechanicBadge("⭐ 4.8+ Rating", "bg-warning bg-opacity-20 text-warning border border-warning", "fa-solid fa-star"));
            }

            if (AvgArrivalTimeMins <= 20)
            {
                list.Add(new MechanicBadge("⚡ Fast Response", "bg-info text-dark", "fa-solid fa-bolt"));
            }

            if (ExperienceYears >= 5 || IsCertified || TotalJobs >= 20)
            {
                list.Add(new MechanicBadge("🛠 Expert Technician", "bg-primary text-white", "fa-solid fa-screwdriver-wrench"));
            }

            if (RecommendationPercentage >= 90 || Rating >= 4.5)
            {
                list.Add(new MechanicBadge("😊 Customer Favourite", "bg-success text-white", "fa-solid fa-heart"));
            }

            if (TotalJobs >= 500)
            {
                list.Add(new MechanicBadge("🏆 500 Jobs Completed", "bg-danger text-white", "fa-solid fa-award"));
            }
            else if (TotalJobs >= 100)
            {
                list.Add(new MechanicBadge("🎯 100 Jobs Completed", "bg-dark border border-secondary text-light", "fa-solid fa-bullseye"));
            }

            return list;
        }

        // Navigation property
        public User? User { get; set; }
    }

    public class MechanicBadge
    {
        public string Name { get; set; } = string.Empty;
        public string CssClass { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;

        public MechanicBadge(string name, string cssClass, string icon)
        {
            Name = name;
            CssClass = cssClass;
            Icon = icon;
        }
    }
}
