using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public interface IPricingService
    {
        Task<List<ProblemTypePricing>> GetAllActiveProblemPricesAsync(string? cityName = null);
        Task<List<PricingRule>> GetAllBaseCategoryPricingRulesAsync();
        Task<bool> UpdateProblemPriceRateAsync(int id, string problemName, string vehicleCategory, string cityName, double minServiceCharge, double maxServiceCharge);
        Task<bool> AddNewProblemPriceRateAsync(string problemName, string vehicleCategory, string cityName, double minServiceCharge, double maxServiceCharge);
        Task<bool> DeleteProblemPriceRateAsync(int id);
        Task<bool> UpdateCategoryBaseRatesAsync(int ruleId, double baseFee, double perKmRate);
    }
}
