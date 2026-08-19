using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.Models
{
    public class Job
    {
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }

        public int? MechanicId { get; set; }

        [Required]
        public int VehicleId { get; set; }

        [Required]
        [StringLength(50)]
        public string ProblemType { get; set; } = string.Empty; 

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Requested"; 

        public string DeclinedMechanicIds { get; set; } = string.Empty;

        [StringLength(50)]
        public string FuelType { get; set; } = "Petrol"; // "Petrol", "CNG", "Electric", "Diesel", "LPG", "Other"

        [StringLength(50)]
        public string BatteryType { get; set; } = "Don't Know"; // "Lead Acid", "Lithium Ion", "Don't Know"

        public bool IsEmergencyCharging { get; set; } = false;

        [StringLength(1000)]
        public string ProblemDescription { get; set; } = string.Empty;

        [StringLength(500)]
        public string ProblemPhotoUrl { get; set; } = string.Empty;

        [StringLength(200)]
        public string Landmark { get; set; } = string.Empty;

        public double CustomerLat { get; set; }
        public double CustomerLng { get; set; }

        [Required]
        [StringLength(500)]
        public string Address { get; set; } = "Current Location";

        // Upfront Pricing details
        public double VisitingCharge { get; set; }
        public double ServiceChargeMin { get; set; }
        public double ServiceChargeMax { get; set; }

        // Custom Estimate & On-Spot Inspection Pricing
        public double CustomEstimateAmount { get; set; } = 0.0;
        public string CustomEstimateDetails { get; set; } = string.Empty;
        public bool? CustomEstimateApproved { get; set; } // null = pending, true = approved, false = rejected

        // Parts / Spares & Extra Labour approval
        public double PartsEstimateAmount { get; set; } = 0.0;
        public double PartsMrp { get; set; } = 0.0;
        public string PartsEstimateDetails { get; set; } = string.Empty;
        public string ExtraPartsName { get; set; } = string.Empty;
        public double ExtraLabourCharge { get; set; } = 0.0;
        public bool? PartsApproved { get; set; } // null = not set, true = approved, false = rejected

        // Towing details
        public bool TowingNeeded { get; set; } = false;
        public double TowingCharge { get; set; } = 0.0;
        public string TowingReason { get; set; } = string.Empty;
        public string TowingProofPhoto { get; set; } = string.Empty; // Simulated photo URL/path
        public bool? TowingApproved { get; set; } // null = not set, true = approved, false = rejected

        public double FinalBillAmount { get; set; } = 0.0;

        // Disputes
        [Required]
        [StringLength(50)]
        public string DisputeStatus { get; set; } = "None"; // "None", "Active", "Resolved"
        public string DisputeReason { get; set; } = string.Empty;
        public string DisputeResolution { get; set; } = string.Empty;

        // Ratings & Feedback
        public double? RatingFromCustomer { get; set; }
        public string FeedbackFromCustomer { get; set; } = string.Empty;
        public string PositiveFeedbackTags { get; set; } = string.Empty;
        public bool? IsRecommended { get; set; } // true = 👍 Yes, false = 👎 No
        [StringLength(500)]
        public string ReviewPhotoUrl { get; set; } = string.Empty;
        public DateTime? RatedAt { get; set; }
        public bool IsFlaggedByAdmin { get; set; } = false;

        public double? RatingFromMechanic { get; set; }
        public string FeedbackFromMechanic { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        // Live Route Simulation & Tracking
        public bool IsSimulationPaused { get; set; } = false;
        public DateTime? LastMovementTime { get; set; }
        public DateTime? LastLocationUpdateTime { get; set; }

        // Navigation properties
        public User? Customer { get; set; }
        public User? Mechanic { get; set; }
        public Vehicle? Vehicle { get; set; }
    }
}
