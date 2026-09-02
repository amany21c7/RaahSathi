using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public class ScoredMechanic
    {
        public User Mechanic { get; set; } = null!;
        public MechanicProfile Profile { get; set; } = null!;
        public double DistanceKm { get; set; }
        public double EtaMinutes { get; set; }
        public double MatchScore { get; set; }
        
        public bool Is5StarPreferred { get; set; } = false;
        
        // Detailed weights for display in the dispatch UI
        public double DistanceWeight { get; set; }
        public double RatingWeight { get; set; }
        public double SkillWeight { get; set; }
        public double AvailabilityWeight { get; set; }
        public double AcceptanceWeight { get; set; }
        public int ActiveJobsCount { get; set; }
    }

    public interface IDispatchEngine
    {
        Task<List<ScoredMechanic>> FindAndRankMechanicsAsync(double customerLat, double customerLng, string vehicleType, string problemType = "", int? customerId = null);
        Task<List<ScoredMechanic>> GetTopParallelDispatchMechanicsAsync(double customerLat, double customerLng, string vehicleType, string problemType = "", int limit = 5, int? customerId = null);
        double CalculateDistance(double lat1, double lon1, double lat2, double lon2);
    }

    public class DispatchEngine : IDispatchEngine
    {
        private readonly ApplicationDbContext _dbContext;

        public DispatchEngine(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<ScoredMechanic>> FindAndRankMechanicsAsync(double customerLat, double customerLng, string vehicleType, string problemType = "", int? customerId = null)
        {
            // Check for previous 5-star rated mechanic within 30 km radius
            int? preferredMechanicId = null;
            if (customerId.HasValue && customerId.Value > 0)
            {
                var prev5StarJob = await _dbContext.Jobs
                    .Where(j => j.CustomerId == customerId.Value && (j.RatingFromCustomer >= 4.8 || j.IsRecommended == true) && j.MechanicId != null)
                    .OrderByDescending(j => j.CreatedAt)
                    .FirstOrDefaultAsync();

                if (prev5StarJob != null && prev5StarJob.MechanicId.HasValue)
                {
                    double prevDist = CalculateDistance(customerLat, customerLng, prev5StarJob.CustomerLat, prev5StarJob.CustomerLng);
                    if (prevDist <= 30.0) // Within 30km radius condition
                    {
                        preferredMechanicId = prev5StarJob.MechanicId.Value;
                    }
                }
            }
            // Find all online mechanics with approved KYC
            var onlineMechanics = await _dbContext.MechanicProfiles
                .Include(m => m.User)
                .Where(m => m.IsOnline && m.KycStatus == "Approved")
                .ToListAsync();

            // Strict Fast Subscription Enforcement: Exclude and auto-offline mechanics whose subscription is due
            bool isSubscriptionMasterEnabled = (await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "SubscriptionEnabled"))?.SettingValue?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            if (isSubscriptionMasterEnabled && onlineMechanics.Count > 0)
            {
                int trialDays = 30;
                var trialSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "SubscriptionFreeTrialDays");
                if (trialSetting != null && int.TryParse(trialSetting.SettingValue, out int tVal)) trialDays = tVal;

                int minJobs = 2;
                var minJobsSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "SubscriptionMinJobsRequired");
                if (minJobsSetting != null && int.TryParse(minJobsSetting.SettingValue, out int jVal)) minJobs = jVal;

                var completedJobCounts = await _dbContext.Jobs
                    .Where(j => j.Status == "Completed" && j.MechanicId != null)
                    .GroupBy(j => j.MechanicId!.Value)
                    .Select(g => new { MechanicId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.MechanicId, x => x.Count);

                var now = DateTime.UtcNow;
                var validMechanics = new List<MechanicProfile>();
                bool anyTurnedOffline = false;

                foreach (var m in onlineMechanics)
                {
                    if (m.User == null) continue;
                    int daysSinceJoined = (int)Math.Max(0, (now - m.User.CreatedAt).TotalDays);
                    int completedJobs = completedJobCounts.ContainsKey(m.UserId) ? completedJobCounts[m.UserId] : 0;

                    if (daysSinceJoined >= trialDays && completedJobs >= minJobs)
                    {
                        if (m.SubscriptionValidTill.HasValue && m.SubscriptionValidTill.Value > now)
                        {
                            validMechanics.Add(m);
                        }
                        else
                        {
                            // Subscription due: Auto-kick offline and exclude from receiving jobs
                            m.IsOnline = false;
                            anyTurnedOffline = true;
                        }
                    }
                    else
                    {
                        validMechanics.Add(m);
                    }
                }

                if (anyTurnedOffline)
                {
                    await _dbContext.SaveChangesAsync();
                }

                onlineMechanics = validMechanics;
            }

            // Fetch active job counts per mechanic to penalize busy mechanics
            var activeJobsPerMechanic = await _dbContext.Jobs
                .Where(j => j.MechanicId != null && (j.Status == "Assigned" || j.Status == "Accepted" || j.Status == "In Progress"))
                .GroupBy(j => j.MechanicId!.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            var scoredList = new List<ScoredMechanic>();

            foreach (var profile in onlineMechanics)
            {
                if (profile.User == null) continue;

                int activeJobs = activeJobsPerMechanic.ContainsKey(profile.UserId) ? activeJobsPerMechanic[profile.UserId] : 0;

                // Calculate distance using Haversine formula
                double distance = CalculateDistance(customerLat, customerLng, profile.Latitude, profile.Longitude);
                
                // Assume 30 km/h average speed in city traffic to get ETA
                double eta = (distance / 30.0) * 60.0; 
                eta = Math.Max(2, Math.Round(eta + 3, 1)); // Minimum 2 minutes + traffic buffer

                // 1. Distance & Proximity Weight (0.35) - Normalizing up to 25 km
                double distanceScore = Math.Max(0, 1 - (distance / 25.0));
                double distanceWeight = distanceScore * 0.35;

                // 2. Skill & Problem Match Weight (0.25)
                double skillScore = 0.3; // Default baseline skill score
                string vTypeLower = (vehicleType ?? "").ToLower();
                string pTypeLower = (problemType ?? "").ToLower();

                bool isErickshawReq = vTypeLower.Contains("e-rickshaw") || vTypeLower.Contains("erickshaw") || vTypeLower.Contains("toto");
                bool isAutoReq = vTypeLower.Contains("auto") || vTypeLower.Contains("3-wheeler");

                if (isErickshawReq)
                {
                    if (!string.IsNullOrEmpty(profile.VehicleExpertise) && profile.VehicleExpertise.Contains("E-Rickshaw", StringComparison.OrdinalIgnoreCase))
                    {
                        skillScore = 0.95; // Top priority for verified E-Rickshaw EV technicians

                        // Check specific EV sub-skills
                        if (!string.IsNullOrEmpty(profile.ErickshawSkills))
                        {
                            var evSkills = profile.ErickshawSkills.ToLower();
                            if (pTypeLower.Contains("controller") && evSkills.Contains("controller")) skillScore = 1.0;
                            else if (pTypeLower.Contains("motor") && evSkills.Contains("motor")) skillScore = 1.0;
                            else if ((pTypeLower.Contains("battery") || pTypeLower.Contains("charging")) && (evSkills.Contains("battery") || evSkills.Contains("charger"))) skillScore = 1.0;
                        }
                    }
                    else
                    {
                        skillScore = 0.15; // Penalty for non-EV mechanics on EV breakdowns
                    }
                }
                else if (isAutoReq)
                {
                    if (!string.IsNullOrEmpty(profile.VehicleExpertise) && (profile.VehicleExpertise.Contains("Auto-Rickshaw", StringComparison.OrdinalIgnoreCase) || profile.VehicleExpertise.Contains("Auto", StringComparison.OrdinalIgnoreCase)))
                    {
                        skillScore = 0.90;
                        if (!string.IsNullOrEmpty(profile.AutoSkills) && profile.AutoSkills.Contains("cng", StringComparison.OrdinalIgnoreCase))
                        {
                            skillScore = 1.0;
                        }
                    }
                    else if (!string.IsNullOrEmpty(profile.SkillCategory) && profile.SkillCategory.ToLower().Contains("2-wheeler"))
                    {
                        skillScore = 0.50;
                    }
                }
                else if (!string.IsNullOrEmpty(profile.SkillCategory))
                {
                    var skills = profile.SkillCategory.Split(',').Select(s => s.Trim().ToLower()).ToList();

                    if (skills.Contains(vTypeLower) || (vTypeLower.Contains("2-wheeler") && skills.Any(s => s.Contains("bike") || s.Contains("scooter"))))
                    {
                        skillScore = 0.85;
                    }
                    else if (skills.Any(s => s.Contains("car") && vTypeLower.Contains("suv")))
                    {
                        skillScore = 0.75;
                    }

                    // Bonus for specific breakdown problem specialization
                    if (!string.IsNullOrEmpty(profile.Specialization))
                    {
                        var specs = profile.Specialization.Split(',').Select(s => s.Trim().ToLower());
                        if (specs.Any(s => pTypeLower.Contains(s) || s.Contains(pTypeLower)))
                        {
                            skillScore = Math.Min(1.0, skillScore + 0.15);
                        }
                    }
                }
                double skillWeight = skillScore * 0.25;

                // 3. Rating & Trust Weight (0.20)
                double ratingScore = Math.Min(1.0, profile.Rating / 5.0);
                double ratingWeight = ratingScore * 0.20;

                // 4. Availability & Active Jobs Weight (0.15)
                double availabilityScore = activeJobs == 0 ? 1.0 : (activeJobs == 1 ? 0.4 : 0.1);
                double availabilityWeight = availabilityScore * 0.15;

                // 5. Acceptance & Performance Rate Weight (0.05)
                double acceptanceScore = (profile.SuccessRatePercentage / 100.0);
                double acceptanceWeight = acceptanceScore * 0.05;

                double totalScore = distanceWeight + skillWeight + ratingWeight + availabilityWeight + acceptanceWeight;
                totalScore = Math.Round(totalScore * 100, 1); // Score out of 100

                bool isPreferred = preferredMechanicId.HasValue && profile.UserId == preferredMechanicId.Value;
                if (isPreferred)
                {
                    totalScore += 500; // Priority Boost for 5-star repeat customer mechanic
                }

                scoredList.Add(new ScoredMechanic
                {
                    Mechanic = profile.User,
                    Profile = profile,
                    DistanceKm = Math.Round(distance, 2),
                    EtaMinutes = eta,
                    MatchScore = totalScore,
                    Is5StarPreferred = isPreferred,
                    DistanceWeight = Math.Round(distanceWeight * 100, 1),
                    RatingWeight = Math.Round(ratingWeight * 100, 1),
                    SkillWeight = Math.Round(skillWeight * 100, 1),
                    AvailabilityWeight = Math.Round(availabilityWeight * 100, 1),
                    AcceptanceWeight = Math.Round(acceptanceWeight * 100, 1),
                    ActiveJobsCount = activeJobs
                });
            }

            // Sort by descending MatchScore
            return scoredList.OrderByDescending(s => s.MatchScore).ToList();
        }

        public async Task<List<ScoredMechanic>> GetTopParallelDispatchMechanicsAsync(double customerLat, double customerLng, string vehicleType, string problemType = "", int limit = 5, int? customerId = null)
        {
            var ranked = await FindAndRankMechanicsAsync(customerLat, customerLng, vehicleType, problemType, customerId);
            return ranked.Take(limit).ToList();
        }

        public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var r = 6371; // Radius of earth in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = r * c; // Distance in km
            return d;
        }

        private double ToRadians(double val)
        {
            return (Math.PI / 180) * val;
        }
    }
}
