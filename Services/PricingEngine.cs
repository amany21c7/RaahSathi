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
        Task<bool> IsCityInEmergencySurgeAsync(string? cityName = null);
    }

    public class PricingEngine : IPricingEngine
    {
        private readonly ApplicationDbContext _dbContext;

        public PricingEngine(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<bool> IsCityInEmergencySurgeAsync(string? cityName = null)
        {
            try
            {
                var globalSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "EmergencyMode");
                bool isGlobal = globalSetting?.SettingValue?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true || globalSetting?.SettingValue?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                if (isGlobal) return true;

                if (!string.IsNullOrWhiteSpace(cityName))
                {
                    string cleanCity = cityName.Trim().ToLower();
                    var cityArea = await _dbContext.CityServiceAreas.FirstOrDefaultAsync(c => c.CityName.ToLower() == cleanCity && c.IsEmergencyMode);
                    if (cityArea != null) return true;
                }
            }
            catch { }

            return false;
        }

        private bool IsCityInEmergencySurgeSync(string? cityName = null)
        {
            try
            {
                var globalSetting = _dbContext.AdminSystemSettings.FirstOrDefault(s => s.SettingKey == "EmergencyMode");
                bool isGlobal = globalSetting?.SettingValue?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true || globalSetting?.SettingValue?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
                if (isGlobal) return true;

                if (!string.IsNullOrWhiteSpace(cityName))
                {
                    string cleanCity = cityName.Trim().ToLower();
                    var cityArea = _dbContext.CityServiceAreas.FirstOrDefault(c => c.CityName.ToLower() == cleanCity && c.IsEmergencyMode);
                    if (cityArea != null) return true;
                }
            }
            catch { }

            return false;
        }

        private static string NormalizeVehicleCategory(string? vehicleType)
        {
            if (string.IsNullOrWhiteSpace(vehicleType)) return "Car";
            string v = vehicleType.Trim().ToLower();

            if (v.Contains("e-rickshaw") || v.Contains("erickshaw") || v.Contains("toto") || (v.Contains("ev") && v.Contains("rickshaw")))
                return "E-Rickshaw";
            if (v.Contains("auto") || v.Contains("cng auto") || v.Contains("tuk tuk") || v.Contains("tempo") || v.Contains("3-wheeler"))
                return "Auto-Rickshaw";
            if (v.Contains("2") || v.Contains("bike") || v.Contains("scooter") || v.Contains("wheeler") || v.Contains("motorcycle") || v.Contains("ev bike"))
                return "2-Wheeler";
            if (v.Contains("comm") || v.Contains("van") || v.Contains("taxi") || v.Contains("pickup"))
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
            double visitingCharge = baseFee + ((distanceKm * 2) * perKmRate);

            // Apply +12% Emergency Weather Surge if City or Global Emergency Mode is Active
            if (await IsCityInEmergencySurgeAsync(cityName))
            {
                baseFee = Math.Round(baseFee * 1.12, 2);
                visitingCharge = Math.Round(visitingCharge * 1.12, 2);
            }

            return (baseFee, Math.Round(visitingCharge, 2));
        }

        public (double min, double max) GetServiceChargeRange(string problemType, string? cityName = null)
        {
            (double min, double max) result = (150, 1000);

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
                        result = (cityMatch.MinServiceCharge, cityMatch.MaxServiceCharge);
                    }
                }

                if (result.min == 150 && result.max == 1000)
                {
                    // 2. Second priority: Fallback to "All Cities" or general rate override
                    var globalMatch = _dbContext.ProblemTypePricings
                        .FirstOrDefault(p => p.IsActive && (p.CityName == "All Cities" || string.IsNullOrEmpty(p.CityName)) && (p.ProblemName.ToLower() == cleanType || p.ProblemName.ToLower().Contains(cleanType) || cleanType.Contains(p.ProblemName.ToLower())));
                    if (globalMatch != null)
                    {
                        result = (globalMatch.MinServiceCharge, globalMatch.MaxServiceCharge);
                    }
                }
            }

            if (result.min == 150 && result.max == 1000)
            {
                result = problemType.ToLower() switch
                {
                    "battery dead / low battery" or "battery dead" or "battery" => (150, 2500),
                    "emergency ev charging" or "charging problem" => (250, 800),
                    "controller problem" or "controller" => (350, 1800),
                    "motor problem" or "bldc motor" => (400, 2500),
                    "battery overheating" => (200, 1200),
                    "wiring / electrical problem" or "electrical problem" or "wiring" => (150, 850),
                    "fuel problem" or "cng problem" or "cng gas problem" or "fuel finished" or "fuel" => (150, 950),
                    "clutch / gear problem" or "gearbox issue" or "gearbox" or "clutch" => (250, 1800),
                    "flat tyre" or "puncture" or "puncture / tyre problem" => (99, 450),
                    "key locked" or "ignition / switch problem" or "lockout" => (150, 500),
                    "suspension issue" or "suspension" or "shocker" => (300, 1800),
                    "brake issue" or "clutch issue" or "brake/clutch" => (180, 800),
                    "engine problem" or "overheating" or "vehicle not starting" or "vehicle not moving" or "starting problem" or "engine" => (250, 2500),
                    _ => (150, 1000)
                };
            }

            // Apply +12% Emergency Surge if active
            if (IsCityInEmergencySurgeSync(cityName))
            {
                result = (Math.Round(result.min * 1.12, 2), Math.Round(result.max * 1.12, 2));
            }

            return result;
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

            if (await IsCityInEmergencySurgeAsync(cityName))
            {
                totalTowing = Math.Round(totalTowing * 1.12, 2);
            }

            return Math.Round(totalTowing, 2);
        }
    }
}
