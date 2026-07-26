using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.Models;
using RaahSathi.Repositories;

namespace RaahSathi.Services
{
    public class PricingService : IPricingService
    {
        private readonly IPricingRepository _pricingRepository;

        public PricingService(IPricingRepository pricingRepository)
        {
            _pricingRepository = pricingRepository;
        }

        public async Task<List<ProblemTypePricing>> GetAllActiveProblemPricesAsync(string? cityName = null)
        {
            return await _pricingRepository.GetAllProblemTypePricingsAsync(cityName);
        }

        public async Task<List<PricingRule>> GetAllBaseCategoryPricingRulesAsync()
        {
            return await _pricingRepository.GetAllPricingRulesAsync();
        }

        public async Task<bool> UpdateProblemPriceRateAsync(int id, string problemName, string vehicleCategory, string cityName, double minServiceCharge, double maxServiceCharge)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(problemName) || minServiceCharge < 0 || maxServiceCharge < minServiceCharge)
            {
                return false;
            }

            return await _pricingRepository.UpdateProblemTypePricingAsync(
                id,
                problemName.Trim(),
                string.IsNullOrWhiteSpace(vehicleCategory) ? "Car" : vehicleCategory.Trim(),
                string.IsNullOrWhiteSpace(cityName) ? "All Cities" : cityName.Trim(),
                minServiceCharge,
                maxServiceCharge
            );
        }

        public async Task<bool> AddNewProblemPriceRateAsync(string problemName, string vehicleCategory, string cityName, double minServiceCharge, double maxServiceCharge)
        {
            if (string.IsNullOrWhiteSpace(problemName) || minServiceCharge < 0 || maxServiceCharge < minServiceCharge)
            {
                return false;
            }

            var newPricing = new ProblemTypePricing
            {
                ProblemName = problemName.Trim(),
                VehicleCategory = string.IsNullOrWhiteSpace(vehicleCategory) ? "Car" : vehicleCategory.Trim(),
                CityName = string.IsNullOrWhiteSpace(cityName) ? "All Cities" : cityName.Trim(),
                MinServiceCharge = minServiceCharge,
                MaxServiceCharge = maxServiceCharge,
                IsActive = true
            };

            return await _pricingRepository.AddProblemTypePricingAsync(newPricing);
        }

        public async Task<bool> DeleteProblemPriceRateAsync(int id)
        {
            if (id <= 0) return false;
            return await _pricingRepository.DeleteProblemTypePricingAsync(id);
        }

        public async Task<bool> UpdateCategoryBaseRatesAsync(int ruleId, string cityName, double baseFee, double perKmRate, double baseTowingFee, double perKmTowingRate)
        {
            if (ruleId <= 0 || baseFee < 0 || perKmRate < 0 || baseTowingFee < 0 || perKmTowingRate < 0) return false;
            return await _pricingRepository.UpdateBasePricingRuleAsync(ruleId, cityName, baseFee, perKmRate, baseTowingFee, perKmTowingRate);
        }
    }
}
