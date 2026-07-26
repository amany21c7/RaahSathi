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
        Task<(double baseFee, double visitingCharge)> CalculateVisitingChargeAsync(string vehicleType, double distanceKm, string? cityName = null);
        (double min, double max) GetServiceChargeRange(string problemType, string? cityName = null);
        Task<double> CalculateTowingChargeAsync(string vehicleType, double distanceKm, string? cityName = null);
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

        public async Task<(double baseFee, double visitingCharge)> CalculateVisitingChargeAsync(string vehicleType, double distanceKm, string? cityName = null)
        {
            string category = NormalizeVehicleCategory(vehicleType);
            string cleanCity = string.IsNullOrWhiteSpace(cityName) ? "" : cityName.Trim().ToLower();

            PricingRule? rule = null;

            // 1. First priority: Check exact match for specific City Name
            if (!string.IsNullOrEmpty(cleanCity))
            {
                rule = await _dbContext.PricingRules
                    .FirstOrDefaultAsync(r => r.VehicleCategory.ToLower() == category.ToLower() && r.CityName.ToLower() == cleanCity);
            }

            // 2. Second priority: Fallback to "All Cities" or default rule
            rule ??= await _dbContext.PricingRules
                .FirstOrDefaultAsync(r => r.VehicleCategory.ToLower() == category.ToLower() && (r.CityName == "All Cities" || string.IsNullOrEmpty(r.CityName)))
                ?? await _dbContext.PricingRules.FirstOrDefaultAsync(r => r.VehicleCategory.ToLower() == category.ToLower())
                ?? await _dbContext.PricingRules.FirstAsync();

            double baseFee = rule.BaseFee;
            double perKmRate = rule.PerKmRate;
            double visitingCharge = baseFee + (distanceKm * perKmRate);

            return (baseFee, Math.Round(visitingCharge, 2));
        }

        public (double min, double max) GetServiceChargeRange(string problemType, string? cityName = null)
        {
            if (!string.IsNullOrWhiteSpace(problemType))
            {
                string cleanType = problemType.Trim().ToLower();
                string cleanCity = string.IsNullOrWhiteSpace(cityName) ? "" : cityName.Trim().ToLower();

                // 1. First priority: Check exact match for specific City Name
                if (!string.IsNullOrEmpty(cleanCity))
                {
                    var cityMatch = _dbContext.ProblemTypePricings
                        .FirstOrDefault(p => p.IsActive && p.CityName.ToLower() == cleanCity && (p.ProblemName.ToLower() == cleanType || p.ProblemName.ToLower().Contains(cleanType) || cleanType.Contains(p.ProblemName.ToLower())));
                    if (cityMatch != null)
                    {
                        return (cityMatch.MinServiceCharge, cityMatch.MaxServiceCharge);
                    }
                }

                // 2. Second priority: Fallback to "All Cities" or general rate override
                var globalMatch = _dbContext.ProblemTypePricings
                    .FirstOrDefault(p => p.IsActive && (p.CityName == "All Cities" || string.IsNullOrEmpty(p.CityName)) && (p.ProblemName.ToLower() == cleanType || p.ProblemName.ToLower().Contains(cleanType) || cleanType.Contains(p.ProblemName.ToLower())));
                if (globalMatch != null)
                {
                    return (globalMatch.MinServiceCharge, globalMatch.MaxServiceCharge);
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

        public async Task<double> CalculateTowingChargeAsync(string vehicleType, double distanceKm, string? cityName = null)
        {
            string category = NormalizeVehicleCategory(vehicleType);
            string cleanCity = string.IsNullOrWhiteSpace(cityName) ? "" : cityName.Trim().ToLower();

            PricingRule? rule = null;

            if (!string.IsNullOrEmpty(cleanCity))
            {
                rule = await _dbContext.PricingRules
                    .FirstOrDefaultAsync(r => r.VehicleCategory.ToLower() == category.ToLower() && r.CityName.ToLower() == cleanCity);
            }

            rule ??= await _dbContext.PricingRules
                .FirstOrDefaultAsync(r => r.VehicleCategory.ToLower() == category.ToLower() && (r.CityName == "All Cities" || string.IsNullOrEmpty(r.CityName)))
                ?? await _dbContext.PricingRules.FirstOrDefaultAsync(r => r.VehicleCategory.ToLower() == category.ToLower())
                ?? await _dbContext.PricingRules.FirstAsync();

            double baseTowing = rule.BaseTowingFee;
            double perKmTowingRate = rule.PerKmTowingRate;
            double totalTowing = baseTowing + (distanceKm * perKmTowingRate);

            return Math.Round(totalTowing, 2);
        }
    }
}
