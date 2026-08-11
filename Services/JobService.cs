using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public class JobService : IJobService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPricingEngine _pricingEngine;
        private readonly IDispatchEngine _dispatchEngine;

        public JobService(ApplicationDbContext dbContext, IPricingEngine pricingEngine, IDispatchEngine dispatchEngine)
        {
            _dbContext = dbContext;
            _pricingEngine = pricingEngine;
            _dispatchEngine = dispatchEngine;
        }

        public async Task<JobDetailDto?> CreateJobAsync(CreateJobRequestDto dto)
        {
            var vehicle = await _dbContext.Vehicles.FindAsync(dto.VehicleId);
            if (vehicle == null) return null;

            // Calculate Upfront Pricing via Pricing Engine
            var visitingResult = await _pricingEngine.CalculateVisitingChargeAsync(vehicle.VehicleType, 5.0, dto.Address);
            var (minEst, maxEst) = _pricingEngine.GetServiceChargeRange(dto.ProblemType, dto.Address);

            double towingCharge = dto.TowingNeeded ? 350.0 : 0.0;
            double finalBill = visitingResult.visitingCharge + minEst + towingCharge;

            var job = new Job
            {
                CustomerId = dto.CustomerId,
                VehicleId = dto.VehicleId,
                ProblemType = dto.ProblemType,
                FuelType = string.IsNullOrWhiteSpace(dto.FuelType) ? "Petrol" : dto.FuelType,
                ProblemDescription = dto.ProblemDescription ?? string.Empty,
                ProblemPhotoUrl = dto.ProblemPhotoUrl ?? string.Empty,
                Landmark = dto.Landmark ?? string.Empty,
                Address = string.IsNullOrWhiteSpace(dto.Address) ? "Current Location" : dto.Address,
                CustomerLat = dto.CustomerLat,
                CustomerLng = dto.CustomerLng,
                VisitingCharge = visitingResult.visitingCharge,
                ServiceChargeMin = minEst,
                ServiceChargeMax = maxEst,
                TowingNeeded = dto.TowingNeeded,
                TowingCharge = towingCharge,
                FinalBillAmount = finalBill,
                Status = "Requested",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Jobs.Add(job);
            await _dbContext.SaveChangesAsync();

            // Run Dispatch Engine to match mechanic
            var rankedMechanics = await _dispatchEngine.FindAndRankMechanicsAsync(dto.CustomerLat, dto.CustomerLng, vehicle.VehicleType, dto.ProblemType, dto.CustomerId);
            var topMatch = rankedMechanics.FirstOrDefault();
            if (topMatch != null && topMatch.Mechanic != null)
            {
                job.MechanicId = topMatch.Mechanic.Id;
                job.Status = "Assigned";
                await _dbContext.SaveChangesAsync();
            }

            return await GetJobDetailsAsync(job.Id);
        }

        public async Task<Job?> GetJobByIdAsync(int jobId)
        {
            return await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Include(j => j.Vehicle)
                .FirstOrDefaultAsync(j => j.Id == jobId);
        }

        public async Task<JobDetailDto?> GetJobDetailsAsync(int jobId)
        {
            var job = await GetJobByIdAsync(jobId);
            if (job == null) return null;

            return MapToJobDetailDto(job);
        }

        public async Task<List<JobDetailDto>> GetCustomerJobsAsync(int customerId)
        {
            var jobs = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Include(j => j.Vehicle)
                .Where(j => j.CustomerId == customerId)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return jobs.Select(MapToJobDetailDto).ToList();
        }

        public async Task<List<JobDetailDto>> GetMechanicJobsAsync(int mechanicId)
        {
            var jobs = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Include(j => j.Vehicle)
                .Where(j => j.MechanicId == mechanicId)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            return jobs.Select(MapToJobDetailDto).ToList();
        }

        public async Task<bool> AcceptJobAsync(int jobId, int mechanicId)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return false;

            job.MechanicId = mechanicId;
            job.Status = "Accepted";
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeclineJobAsync(int jobId, int mechanicId)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return false;

            string existingDeclined = job.DeclinedMechanicIds ?? "";
            if (!existingDeclined.Split(',').Contains(mechanicId.ToString()))
            {
                job.DeclinedMechanicIds = string.IsNullOrEmpty(existingDeclined) ? mechanicId.ToString() : $"{existingDeclined},{mechanicId}";
            }

            job.MechanicId = null;
            job.Status = "Requested";
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UpdateJobStatusAsync(int jobId, string status)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return false;

            job.Status = status;
            if (status == "Completed")
            {
                job.CompletedAt = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CancelJobAsync(int jobId, string reason)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return false;

            job.Status = "Cancelled";
            job.DisputeReason = reason;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        private static JobDetailDto MapToJobDetailDto(Job job)
        {
            return new JobDetailDto
            {
                Id = job.Id,
                CustomerId = job.CustomerId,
                CustomerName = job.Customer?.Name ?? "Customer",
                CustomerPhone = job.Customer?.PhoneNumber ?? "",
                MechanicId = job.MechanicId,
                MechanicName = job.Mechanic?.Name ?? "Pending Assignment",
                MechanicPhone = job.Mechanic?.PhoneNumber ?? "",
                VehicleId = job.VehicleId,
                VehicleModel = job.Vehicle != null ? job.Vehicle.Model : "Vehicle",
                RegistrationNumber = job.Vehicle?.RegistrationNumber ?? "",
                ProblemType = job.ProblemType,
                Status = job.Status,
                Address = job.Address,
                VisitingCharge = job.VisitingCharge,
                ServiceChargeMin = job.ServiceChargeMin,
                ServiceChargeMax = job.ServiceChargeMax,
                CustomEstimateAmount = job.CustomEstimateAmount,
                PartsEstimateAmount = job.PartsEstimateAmount,
                TowingCharge = job.TowingCharge,
                FinalBillAmount = job.FinalBillAmount,
                CreatedAt = job.CreatedAt,
                CompletedAt = job.CompletedAt
            };
        }
    }
}
