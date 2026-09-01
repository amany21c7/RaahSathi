using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;
using RaahSathi.Repositories;

namespace RaahSathi.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IPaymentRepository _paymentRepository;
        private readonly ApplicationDbContext _dbContext;
        private readonly IReferralService _referralService;

        public PaymentService(IPaymentRepository paymentRepository, ApplicationDbContext dbContext, IReferralService referralService)
        {
            _paymentRepository = paymentRepository;
            _dbContext = dbContext;
            _referralService = referralService;
        }

        private double GetSettingDouble(string key, double defaultValue)
        {
            try
            {
                var setting = _dbContext.AdminSystemSettings.FirstOrDefault(s => s.SettingKey == key);
                if (setting != null && double.TryParse(setting.SettingValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                {
                    return val;
                }
            }
            catch { }
            return defaultValue;
        }

        public PaymentCommissionCalculationResult CalculateTieredCommissionAndNetEarnings(double totalBillAmount, double partsAmount = 0, double customRepairAmount = 0)
        {
            // Fetch rates from DB settings or fall back to default
            double rate1 = GetSettingDouble("CommissionPhase1", 8) / 100.0;
            double rate2 = GetSettingDouble("CommissionPhase2", 10) / 100.0;
            double rate3 = GetSettingDouble("CommissionPhase3", 12) / 100.0;
            double rateParts = GetSettingDouble("CommissionParts", 5) / 100.0;
            double rateCustomRepair = GetSettingDouble("CommissionCustomRepair", 0) / 100.0;

            double serviceAmount = totalBillAmount - partsAmount;
            if (serviceAmount < 0) serviceAmount = 0;

            double serviceCommRate = 0.08;
            double serviceCommission = 0;

            if (totalBillAmount < 1000)
            {
                serviceCommRate = rate1;
                serviceCommission = totalBillAmount * rate1;
            }
            else if (totalBillAmount <= 3000)
            {
                serviceCommRate = rate2;
                serviceCommission = totalBillAmount * rate2;
            }
            else
            {
                serviceCommRate = rate3;
                serviceCommission = totalBillAmount * rate3;
            }

            double partsCommission = partsAmount * rateParts;
            double customRepairCommission = (customRepairAmount > 0 && rateCustomRepair > 0)
                ? (customRepairAmount * rateCustomRepair)
                : 0;

            double totalCommission = Math.Round(serviceCommission + partsCommission + customRepairCommission, 2);
            double mechanicNetEarning = Math.Round(totalBillAmount - totalCommission, 2);

            // Compute effective average rate for backward compatibility
            double effectiveRate = totalBillAmount > 0 ? (totalCommission / totalBillAmount) : serviceCommRate;

            return new PaymentCommissionCalculationResult
            {
                TotalBillAmount = totalBillAmount,
                CommissionRate = effectiveRate,
                AdminCommissionAmount = totalCommission,
                MechanicNetEarningAmount = mechanicNetEarning
            };
        }

        public async Task<bool> ProcessEscrowPaymentForJobAsync(int jobId, string? paymentId)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return false;

            // Idempotency Check: if payment is already processed and released, do not process again
            var existingPayment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.JobId == jobId);
            if (existingPayment != null && (existingPayment.PaymentStatus == "Released" || existingPayment.PaymentStatus == "Completed"))
            {
                return false;
            }

            string payId = string.IsNullOrWhiteSpace(paymentId) ? "pay_" + Guid.NewGuid().ToString().Substring(0, 14) : paymentId.Trim();

            double baseEst = job.VisitingCharge + job.ServiceChargeMin;
            double finalBill = job.FinalBillAmount > baseEst ? job.FinalBillAmount : baseEst;
            double partsAmt = (job.PartsApproved == true) ? job.PartsEstimateAmount : 0;
            double customAmt = (job.CustomEstimateApproved == true) ? job.CustomEstimateAmount : 0;
            var commCalc = CalculateTieredCommissionAndNetEarnings(finalBill, partsAmt, customAmt);

            bool isCash = payId.StartsWith("pay_cash_", StringComparison.OrdinalIgnoreCase);
            double actualMechanicEarning = isCash ? -commCalc.AdminCommissionAmount : commCalc.MechanicNetEarningAmount;

            // Try executing SQL Server Stored Procedure first via Repository Layer
            bool spSuccess = await _paymentRepository.ExecuteProcessJobPaymentStoredProcedureAsync(
                job.Id, 
                payId, 
                finalBill, 
                commCalc.AdminCommissionAmount, 
                actualMechanicEarning, 
                commCalc.CommissionRate
            );

            if (spSuccess)
            {
                try
                {
                    await _referralService.ProcessJobCompletionReferralRewardAsync(job.Id);
                }
                catch { }
                return true;
            }

            // Fallback: direct EF context transaction
            var paymentModel = new Payment
            {
                JobId = job.Id,
                Amount = finalBill,
                PaymentStatus = "Released",
                RazorpayPaymentId = payId,
                AdminCommissionAmount = commCalc.AdminCommissionAmount,
                MechanicEarningAmount = actualMechanicEarning,
                CommissionRateUsed = commCalc.CommissionRate,
                CreatedAt = DateTime.UtcNow
            };

            await _paymentRepository.SaveEscrowPaymentWithFallbackAsync(
                paymentModel,
                job.Id,
                job.MechanicId,
                actualMechanicEarning,
                commCalc.CommissionRate
            );

            return true;
        }

        public async Task<JobInvoiceBreakdownResult> GenerateJobInvoiceBreakdownAsync(int jobId)
        {
            var job = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
            {
                return new JobInvoiceBreakdownResult
                {
                    Success = false,
                    Message = "Job record not found."
                };
            }

            var mechProfile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == job.MechanicId);
            var payment = await _paymentRepository.GetPaymentByJobIdAsync(jobId);

            double baseEstBill = job.VisitingCharge + job.ServiceChargeMin;
            double totalBill = job.FinalBillAmount > baseEstBill ? job.FinalBillAmount : baseEstBill;
            double partsAmt = (job.PartsApproved == true) ? job.PartsEstimateAmount : 0;
            double customAmt = (job.CustomEstimateApproved == true) ? job.CustomEstimateAmount : 0;
            var commCalc = CalculateTieredCommissionAndNetEarnings(totalBill, partsAmt, customAmt);

            double adminCommission = payment != null && payment.AdminCommissionAmount > 0 
                ? payment.AdminCommissionAmount 
                : commCalc.AdminCommissionAmount;

            double mechanicNetEarning = payment != null && payment.MechanicEarningAmount > 0 
                ? payment.MechanicEarningAmount 
                : commCalc.MechanicNetEarningAmount;

            double effectiveCommRatePct = (payment?.CommissionRateUsed ?? commCalc.CommissionRate) * 100;

            return new JobInvoiceBreakdownResult
            {
                Success = true,
                InvoiceNo = $"RS-INV-{job.Id:D4}-{DateTime.Now.Year}",
                JobId = job.Id,
                Date = (job.CompletedAt ?? job.CreatedAt).ToString("dd MMM yyyy, hh:mm tt"),
                Status = job.Status,
                CustomerName = job.Customer?.Name ?? "Customer",
                CustomerPhone = job.Customer?.PhoneNumber ?? "N/A",
                CustomerAddress = job.Address ?? "Breakdown Location",
                VehicleModel = job.Vehicle?.Model ?? "Vehicle",
                VehicleType = job.Vehicle?.VehicleType ?? "Car",
                VehicleRegNumber = job.Vehicle?.RegistrationNumber ?? "UP32 AB 1234",
                FuelType = job.FuelType ?? "Petrol",
                MechanicName = job.Mechanic?.Name ?? "Verified Technician",
                MechanicPhone = job.Mechanic?.PhoneNumber ?? "N/A",
                ShopName = mechProfile?.ShopName ?? "RaahSathi Partner Garage",
                ShopAddress = mechProfile?.ShopAddress ?? "Sector 62 Noida",
                ProblemType = job.ProblemType ?? "Roadside Emergency",

                // Breakdown Items
                VisitingCharge = job.VisitingCharge,
                ServiceChargeMin = job.ServiceChargeMin,
                CustomEstimateAmount = job.CustomEstimateApproved == true ? job.CustomEstimateAmount : 0.0,
                CustomEstimateDetails = job.CustomEstimateDetails,
                PartsEstimateAmount = job.PartsApproved == true ? job.PartsEstimateAmount : 0.0,
                PartsMrp = job.PartsApproved == true ? (job.PartsMrp > 0 ? job.PartsMrp : job.PartsEstimateAmount) : 0.0,
                ExtraLabourCharge = job.PartsApproved == true ? job.ExtraLabourCharge : 0.0,
                PartsDetails = job.ExtraPartsName,
                TowingCharge = job.TowingApproved == true ? job.TowingCharge : 0.0,

                // Financial Totals
                TotalBillAmount = totalBill,
                AdminCommission = adminCommission,
                MechanicNetEarning = mechanicNetEarning,
                CommissionPercent = effectiveCommRatePct
            };
        }

        public async Task<List<Payment>> GetAdminEscrowTransactionsLedgerAsync()
        {
            return await _paymentRepository.GetAllEscrowTransactionsAsync();
        }
    }
}
