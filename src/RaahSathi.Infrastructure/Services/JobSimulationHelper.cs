using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public static class JobSimulationHelper
    {
        public static async Task SimulateMovementAsync(ApplicationDbContext dbContext, Job job)
        {
            if (job == null || job.Status != "Driving" || !job.MechanicId.HasValue || job.IsSimulationPaused)
            {
                return;
            }

            var mechProfile = await dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == job.MechanicId.Value);
            if (mechProfile == null)
            {
                return;
            }

            var now = DateTime.UtcNow;

            // Initialize timestamps if null
            if (!job.LastMovementTime.HasValue)
            {
                job.LastMovementTime = now;
            }
            if (!job.LastLocationUpdateTime.HasValue)
            {
                job.LastLocationUpdateTime = now;
            }

            // Update movement only once every 2 seconds to make updates structured
            var elapsedSeconds = (now - job.LastLocationUpdateTime.Value).TotalSeconds;
            if (elapsedSeconds >= 2.0)
            {
                double diffLat = job.CustomerLat - mechProfile.Latitude;
                double diffLng = job.CustomerLng - mechProfile.Longitude;

                // Check if already reached
                if (Math.Abs(diffLat) < 0.00015 && Math.Abs(diffLng) < 0.00015)
                {
                    mechProfile.Latitude = job.CustomerLat;
                    mechProfile.Longitude = job.CustomerLng;
                }
                else
                {
                    // Move 12% closer per check
                    mechProfile.Latitude += diffLat * 0.12;
                    mechProfile.Longitude += diffLng * 0.12;
                }

                // Update tracking timestamps since movement happened
                job.LastLocationUpdateTime = now;
                job.LastMovementTime = now;

                await dbContext.SaveChangesAsync();
            }
        }
    }
}
