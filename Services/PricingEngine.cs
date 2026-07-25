using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public interface IPricingEngine
    {
        Task<(double baseFee, double visitingCharge)> CalculateVisitingChargeAsync(string vehicleType, double distanceKm);
        (double min, double max) GetServiceChargeRange(string problemType);
        Task<double> CalculateTowingChargeAsync(string vehicleType, double distanceKm);
    }

    public class PricingEngine : IPricingEngine
    {
        private readonly ApplicationDbContext _dbContext;

        public PricingEngine(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private static string NormalizeVehicleCategory(string? vehicleType)
        {
            if (string.IsNullOrWhiteSpace(vehicleType)) return "Car";
            string v = vehicleType.Trim().ToLower();

            if (v.Contains("2") || v.Contains("bike") || v.Contains("scooter") || v.Contains("wheeler") || v.Contains("motorcycle") || v.Contains("ev bike"))
                return "2-Wheeler";
            if (v.Contains("comm") || v.Contains("auto") || v.Contains("rickshaw") || v.Contains("van") || v.Contains("taxi"))
                return "Commercial";
            if (v.Contains("heavy") || v.Contains("truck") || v.Contains("bus") || v.Contains("jcb") || v.Contains("crane") || v.Contains("tractor"))
                return "Heavy";

            return "Car";
        }

        public async Task<(double baseFee, double visitingCharge)> CalculateVisitingChargeAsync(string vehicleType, double distanceKm)
        {
            string category = NormalizeVehicleCategory(vehicleType);

            var rule = await _dbContext.PricingRules
                .FirstOrDefaultAsync(r => r.VehicleCategory.ToLower() == category.ToLower())
                ?? await _dbContext.PricingRules.FirstOrDefaultAsync(r => r.VehicleCategory.ToLower().Contains(category.ToLower()))
                ?? await _dbContext.PricingRules.FirstAsync(); // fallback to default

            double baseFee = rule.BaseFee;
            double perKmRate = rule.PerKmRate;
            double visitingCharge = baseFee + (distanceKm * perKmRate);

            return (baseFee, Math.Round(visitingCharge, 2));
        }

        public (double min, double max) GetServiceChargeRange(string problemType)
        {
            if (!string.IsNullOrWhiteSpace(problemType))
            {
                string cleanType = problemType.Trim().ToLower();
                
                var exactMatch = _dbContext.ProblemTypePricings
                    .FirstOrDefault(p => p.IsActive && p.ProblemName.ToLower() == cleanType);
                if (exactMatch != null)
                {
                    return (exactMatch.MinServiceCharge, exactMatch.MaxServiceCharge);
                }

                var partialMatch = _dbContext.ProblemTypePricings
                    .FirstOrDefault(p => p.IsActive && (p.ProblemName.ToLower().Contains(cleanType) || cleanType.Contains(p.ProblemName.ToLower())));
                if (partialMatch != null)
                {
                    return (partialMatch.MinServiceCharge, partialMatch.MaxServiceCharge);
                }
            }

            return problemType.ToLower() switch
            {
                "battery dead" or "battery" => (150, 3500),
                "flat tyre" or "puncture" => (100, 400),
                "fuel finished" or "fuel" => (100, 1100),
                "key locked" or "lockout" => (200, 500),
                "gearbox issue" or "gearbox" or "clutch" => (400, 2500),
                "suspension issue" or "suspension" or "shocker" => (350, 2200),
                "brake issue" or "clutch issue" or "brake/clutch" => (200, 800),
                "engine problem" or "overheating" or "starting problem" or "engine" => (300, 3000),
                _ => (150, 1000)
            };
        }

        public async Task<double> CalculateTowingChargeAsync(string vehicleType, double distanceKm)
        {
            string category = NormalizeVehicleCategory(vehicleType);

            var rule = await _dbContext.PricingRules
                .FirstOrDefaultAsync(r => r.VehicleCategory.ToLower() == category.ToLower())
                ?? await _dbContext.PricingRules.FirstOrDefaultAsync(r => r.VehicleCategory.ToLower().Contains(category.ToLower()))
                ?? await _dbContext.PricingRules.FirstAsync(); // fallback

            double baseTowing = rule.BaseTowingFee;
            double perKmTowingRate = rule.PerKmTowingRate;
            double totalTowing = baseTowing + (distanceKm * perKmTowingRate);

            return Math.Round(totalTowing, 2);
        }
    }
}
