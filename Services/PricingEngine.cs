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

        public async Task<(double baseFee, double visitingCharge)> CalculateVisitingChargeAsync(string vehicleType, double distanceKm)
        {
            var rule = await _dbContext.PricingRules
                .FirstOrDefaultAsync(r => r.VehicleCategory == vehicleType)
                ?? await _dbContext.PricingRules.FirstAsync(); // fallback to default

            double baseFee = rule.BaseFee;
            double perKmRate = rule.PerKmRate;
            double visitingCharge = baseFee + (distanceKm * perKmRate);

            return (baseFee, Math.Round(visitingCharge, 2));
        }

        public (double min, double max) GetServiceChargeRange(string problemType)
        {
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
            var rule = await _dbContext.PricingRules
                .FirstOrDefaultAsync(r => r.VehicleCategory == vehicleType)
                ?? await _dbContext.PricingRules.FirstAsync(); // fallback

            double baseTowing = rule.BaseTowingFee;
            double perKmTowingRate = rule.PerKmTowingRate;
            double totalTowing = baseTowing + (distanceKm * perKmTowingRate);

            return Math.Round(totalTowing, 2);
        }
    }
}
