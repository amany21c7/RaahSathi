using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;

namespace RaahSathi.Repositories
{
    public class PaymentRepository : IPaymentRepository
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Services.IReferralService _referralService;

        public PaymentRepository(ApplicationDbContext dbContext, Services.IReferralService referralService)
        {
            _dbContext = dbContext;
            _referralService = referralService;
        }

        public async Task<Payment?> GetPaymentByJobIdAsync(int jobId)
        {
            return await _dbContext.Payments
                .Include(p => p.Job)
                .FirstOrDefaultAsync(p => p.JobId == jobId);
        }

        public async Task<List<Payment>> GetAllEscrowTransactionsAsync()
        {
            return await _dbContext.Payments
                .Include(p => p.Job)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ExecuteProcessEscrowStoredProcedureAsync(int jobId, string paymentId)
        {
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "EXEC dbo.rs_payments_process_escrow @JobId = {0}, @PaymentId = {1}",
                    jobId, paymentId
                );
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task SaveEscrowPaymentWithFallbackAsync(Payment payment, int jobId, int? mechanicId, double mechanicEarning, double commissionRate)
        {
            var existingPayment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.JobId == jobId);
            if (existingPayment != null)
            {
                existingPayment.Amount = payment.Amount;
                existingPayment.PaymentStatus = payment.PaymentStatus;
                existingPayment.RazorpayPaymentId = payment.RazorpayPaymentId;
                existingPayment.AdminCommissionAmount = payment.AdminCommissionAmount;
                existingPayment.MechanicEarningAmount = payment.MechanicEarningAmount;
                existingPayment.CommissionRateUsed = payment.CommissionRateUsed;
            }
            else
            {
                _dbContext.Payments.Add(payment);
            }

            if (mechanicId.HasValue)
            {
                var mechProf = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == mechanicId.Value);
                if (mechProf != null)
                {
                    mechProf.CurrentEarnings += mechanicEarning;
                    mechProf.TotalJobs += 1;
                }
            }

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job != null)
            {
                job.Status = "Completed";
                job.CompletedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();

            try
            {
                await _referralService.ProcessJobCompletionReferralRewardAsync(jobId);
            }
            catch { }
        }
    }
}
