using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.DTOs
{
    public class CreateJobRequestDto
    {
        [Required]
        public int CustomerId { get; set; }

        [Required]
        public int VehicleId { get; set; }

        [Required]
        public string ProblemType { get; set; } = string.Empty;

        public string FuelType { get; set; } = "Petrol";
        public string ProblemDescription { get; set; } = string.Empty;
        public string ProblemPhotoUrl { get; set; } = string.Empty;
        public string Landmark { get; set; } = string.Empty;
        public string Address { get; set; } = "Current Location";

        public double CustomerLat { get; set; }
        public double CustomerLng { get; set; }

        public bool TowingNeeded { get; set; }
    }

    public class JobDetailDto
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public int? MechanicId { get; set; }
        public string MechanicName { get; set; } = string.Empty;
        public string MechanicPhone { get; set; } = string.Empty;
        public int VehicleId { get; set; }
        public string VehicleModel { get; set; } = string.Empty;
        public string RegistrationNumber { get; set; } = string.Empty;
        public string ProblemType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double VisitingCharge { get; set; }
        public double ServiceChargeMin { get; set; }
        public double ServiceChargeMax { get; set; }
        public double CustomEstimateAmount { get; set; }
        public double PartsEstimateAmount { get; set; }
        public double TowingCharge { get; set; }
        public double FinalBillAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class JobStatusUpdateDto
    {
        [Required]
        public int JobId { get; set; }
        [Required]
        public string Status { get; set; } = string.Empty;
        public int? MechanicId { get; set; }
    }

    public class AcceptJobRequestDto
    {
        [Required]
        public int JobId { get; set; }
        [Required]
        public int MechanicId { get; set; }
    }

    public class DeclineJobRequestDto
    {
        [Required]
        public int JobId { get; set; }
        [Required]
        public int MechanicId { get; set; }
    }
}
