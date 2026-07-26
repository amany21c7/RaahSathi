using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.Models;

namespace RaahSathi.Repositories
{
    public interface IPricingRepository
    {
        Task<List<ProblemTypePricing>> GetAllProblemTypePricingsAsync(string? cityName = null);
        Task<ProblemTypePricing?> GetProblemTypePricingByIdAsync(int id);
        Task<bool> UpdateProblemTypePricingAsync(int id, string problemName, string category, string cityName, double minCharge, double maxCharge);
        Task<bool> AddProblemTypePricingAsync(ProblemTypePricing pricing);
        Task<bool> DeleteProblemTypePricingAsync(int id);
        Task<List<PricingRule>> GetAllPricingRulesAsync();
        Task<bool> UpdateBasePricingRuleAsync(int ruleId, double baseFee, double perKmRate);
    }
}
