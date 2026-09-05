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
            try
            {
                return await _dbContext.Payments
                    .Include(p => p.Job)
                    .FirstOrDefaultAsync(p => p.JobId == jobId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<Payment>> GetAllEscrowTransactionsAsync()
        {
            try
            {
                return await _dbContext.Payments
                    .Include(p => p.Job)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<Payment>();
            }
        }

        public async Task<bool> ExecuteProcessJobPaymentStoredProcedureAsync(
            int jobId, 
            string paymentId, 
            double amount, 
            double adminCommission, 
            double mechanicEarning, 
            double commissionRate)
        {
            try
            {
                if (_dbContext.Database.IsSqlServer())
                {
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        "EXEC dbo.rs_payments_process_job @JobId = {0}, @PaymentId = {1}, @Amount = {2}, @AdminCommission = {3}, @MechanicEarning = {4}, @CommissionRate = {5}, @PaymentStatus = {6}",
                        jobId, paymentId, amount, adminCommission, mechanicEarning, commissionRate, "Released"
                    );
                    return true;
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task SaveEscrowPaymentWithFallbackAsync(Payment payment, int jobId, int? mechanicId, double mechanicEarning, double commissionRate)
        {
            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
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
                    await transaction.CommitAsync();

                    try
                    {
                        await _referralService.ProcessJobCompletionReferralRewardAsync(jobId);
                    }
                    catch { }
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                }
            }
            catch (Exception)
            {
                // Graceful handling without breaking the calling flow
            }
        }
    }
}
