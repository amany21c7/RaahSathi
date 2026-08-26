using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using RaahSathi.Models;
using RaahSathi.Repositories;

namespace RaahSathi.Services
{
    public class PricingService : IPricingService
    {
        private readonly IPricingRepository _pricingRepository;
        private readonly IMemoryCache _cache;
        private const string CacheKeyRules = "RaahSathi_PricingRules_All";

        public PricingService(IPricingRepository pricingRepository, IMemoryCache cache)
        {
            _pricingRepository = pricingRepository;
            _cache = cache;
        }

        public async Task<List<ProblemTypePricing>> GetAllActiveProblemPricesAsync(string? cityName = null)
        {
            string cacheKey = $"RaahSathi_ProblemPrices_{cityName?.Trim().ToLower() ?? "all"}";
            if (_cache.TryGetValue(cacheKey, out List<ProblemTypePricing>? cached) && cached != null)
            {
                return cached;
            }

            var prices = await _pricingRepository.GetAllProblemTypePricingsAsync(cityName);
            _cache.Set(cacheKey, prices, TimeSpan.FromMinutes(15));
            return prices;
        }

        public async Task<List<PricingRule>> GetAllBaseCategoryPricingRulesAsync()
        {
            if (_cache.TryGetValue(CacheKeyRules, out List<PricingRule>? cached) && cached != null)
            {
                return cached;
            }

            var rules = await _pricingRepository.GetAllPricingRulesAsync();
            _cache.Set(CacheKeyRules, rules, TimeSpan.FromMinutes(15));
            return rules;
        }

        private void InvalidateCache()
        {
            _cache.Remove(CacheKeyRules);
            // Invalidate rules cache on updates
        }

        public async Task<bool> UpdateProblemPriceRateAsync(int id, string problemName, string vehicleCategory, string cityName, double minServiceCharge, double maxServiceCharge)
        {
            if (id <= 0 || string.IsNullOrWhiteSpace(problemName) || minServiceCharge < 0 || maxServiceCharge < minServiceCharge)
            {
                return false;
            }

            var result = await _pricingRepository.UpdateProblemTypePricingAsync(
                id,
                problemName.Trim(),
                string.IsNullOrWhiteSpace(vehicleCategory) ? "Car" : vehicleCategory.Trim(),
                string.IsNullOrWhiteSpace(cityName) ? "All Cities" : cityName.Trim(),
                minServiceCharge,
                maxServiceCharge
            );

            if (result) InvalidateCache();
            return result;
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

            var result = await _pricingRepository.AddProblemTypePricingAsync(newPricing);
            if (result) InvalidateCache();
            return result;
        }

        public async Task<bool> DeleteProblemPriceRateAsync(int id)
        {
            if (id <= 0) return false;
            var result = await _pricingRepository.DeleteProblemTypePricingAsync(id);
            if (result) InvalidateCache();
            return result;
        }

        public async Task<bool> UpdateCategoryBaseRatesAsync(int ruleId, string cityName, double baseFee, double perKmRate, double baseTowingFee, double perKmTowingRate)
        {
            if (ruleId <= 0 || baseFee < 0 || perKmRate < 0 || baseTowingFee < 0 || perKmTowingRate < 0) return false;
            var result = await _pricingRepository.UpdateBasePricingRuleAsync(ruleId, cityName, baseFee, perKmRate, baseTowingFee, perKmTowingRate);
            if (result) InvalidateCache();
            return result;
        }
    }
}
