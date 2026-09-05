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
            if (job == null || (job.Status != "Driving" && job.Status != "Accepted" && job.Status != "Assigned") || !job.MechanicId.HasValue || job.IsSimulationPaused)
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

            // Update movement smoothly every 2.5 seconds
            var elapsedSeconds = (now - job.LastLocationUpdateTime.Value).TotalSeconds;
            if (elapsedSeconds >= 2.5)
            {
                double diffLat = job.CustomerLat - mechProfile.Latitude;
                double diffLng = job.CustomerLng - mechProfile.Longitude;

                // Check if reached destination (within 20 meters)
                if (Math.Abs(diffLat) < 0.0002 && Math.Abs(diffLng) < 0.0002)
                {
                    mechProfile.Latitude = job.CustomerLat;
                    mechProfile.Longitude = job.CustomerLng;
                }
                else
                {
                    // Move smoothly 4% closer per 2.5 seconds (gives ~60-90 seconds of realistic live street navigation)
                    mechProfile.Latitude += diffLat * 0.04;
                    mechProfile.Longitude += diffLng * 0.04;
                }

                // Update tracking timestamps since movement happened
                job.LastLocationUpdateTime = now;
                job.LastMovementTime = now;

                await dbContext.SaveChangesAsync();
            }
        }
    }
}
