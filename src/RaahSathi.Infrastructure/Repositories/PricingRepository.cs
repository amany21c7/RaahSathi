using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;

namespace RaahSathi.Repositories
{
    public class PricingRepository : IPricingRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public PricingRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<ProblemTypePricing>> GetAllProblemTypePricingsAsync(string? cityName = null)
        {
            var query = _dbContext.ProblemTypePricings.Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(cityName) && !cityName.Equals("All Cities", System.StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(p => p.CityName.ToLower() == cityName.Trim().ToLower() || p.CityName == "All Cities");
            }

            return await query
                .OrderBy(p => p.CityName)
                .ThenBy(p => p.VehicleCategory)
                .ThenBy(p => p.ProblemName)
                .ToListAsync();
        }

        public async Task<ProblemTypePricing?> GetProblemTypePricingByIdAsync(int id)
        {
            return await _dbContext.ProblemTypePricings.FindAsync(id);
        }

        public async Task<bool> UpdateProblemTypePricingAsync(int id, string problemName, string category, string cityName, double minCharge, double maxCharge)
        {
            var problem = await _dbContext.ProblemTypePricings.FindAsync(id);
            if (problem == null) return false;

            problem.ProblemName = problemName.Trim();
            problem.VehicleCategory = category.Trim();
            problem.CityName = string.IsNullOrWhiteSpace(cityName) ? "All Cities" : cityName.Trim();
            problem.MinServiceCharge = minCharge;
            problem.MaxServiceCharge = maxCharge;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> AddProblemTypePricingAsync(ProblemTypePricing pricing)
        {
            _dbContext.ProblemTypePricings.Add(pricing);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteProblemTypePricingAsync(int id)
        {
            var problem = await _dbContext.ProblemTypePricings.FindAsync(id);
            if (problem == null) return false;

            _dbContext.ProblemTypePricings.Remove(problem);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<List<PricingRule>> GetAllPricingRulesAsync()
        {
            return await _dbContext.PricingRules.ToListAsync();
        }

        public async Task<bool> UpdateBasePricingRuleAsync(int ruleId, string cityName, double baseFee, double perKmRate, double baseTowingFee, double perKmTowingRate)
        {
            var rule = await _dbContext.PricingRules.FindAsync(ruleId);
            if (rule == null) return false;

            rule.CityName = string.IsNullOrWhiteSpace(cityName) ? "All Cities" : cityName.Trim();
            rule.BaseFee = baseFee;
            rule.PerKmRate = perKmRate;
            rule.BaseTowingFee = baseTowingFee;
            rule.PerKmTowingRate = perKmTowingRate;

            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
