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
    public class WalletService : IWalletService
    {
        private readonly ApplicationDbContext _dbContext;

        public WalletService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<WalletBalanceDto> GetWalletBalanceAsync(int mechanicId)
        {
            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == mechanicId);
            double currentBalance = profile?.CurrentEarnings ?? 0.0;
            int totalJobs = profile?.TotalJobs ?? 0;

            double pendingPayouts = await _dbContext.MechanicPayoutRequests
                .Where(r => r.MechanicId == mechanicId && r.Status == "Pending")
                .SumAsync(r => (double?)r.Amount) ?? 0.0;

            double completedPayouts = await _dbContext.MechanicPayoutRequests
                .Where(r => r.MechanicId == mechanicId && r.Status == "Approved")
                .SumAsync(r => (double?)r.Amount) ?? 0.0;

            return new WalletBalanceDto
            {
                MechanicId = mechanicId,
                CurrentBalance = currentBalance,
                PendingPayoutAmount = pendingPayouts,
                LifetimeEarnings = currentBalance + completedPayouts,
                TotalJobsCompleted = totalJobs
            };
        }

        public async Task<PayoutResponseDto> RequestPayoutAsync(CreatePayoutRequestDto request)
        {
            if (request.Amount < 100)
            {
                return new PayoutResponseDto { Success = false, Message = "Minimum payout withdrawal amount is ₹100." };
            }

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == request.MechanicId);
            if (profile == null)
            {
                return new PayoutResponseDto { Success = false, Message = "Mechanic profile not found." };
            }

            if (profile.CurrentEarnings < request.Amount)
            {
                return new PayoutResponseDto
                {
                    Success = false,
                    Message = $"Insufficient wallet balance. Available: ₹{profile.CurrentEarnings:F2}, Requested: ₹{request.Amount:F2}"
                };
            }

            // Deduct balance from mechanic wallet immediately or hold it
            profile.CurrentEarnings -= request.Amount;

            var payoutRequest = new MechanicPayoutRequest
            {
                MechanicId = request.MechanicId,
                Amount = request.Amount,
                PayoutMethod = string.IsNullOrWhiteSpace(request.PayoutMethod) ? "UPI" : request.PayoutMethod,
                AccountHolderName = request.AccountHolderName ?? profile.AccountHolderName,
                BankAccountNumber = request.BankAccountNumber ?? profile.BankAccountNumber,
                BankName = request.BankName ?? profile.BankName,
                IfscCode = request.IfscCode ?? profile.IfscCode,
                UpiId = request.UpiId ?? profile.UpiId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.MechanicPayoutRequests.Add(payoutRequest);
            await _dbContext.SaveChangesAsync();

            return new PayoutResponseDto
            {
                Success = true,
                Message = "Payout request submitted successfully. Admin review pending.",
                PayoutRequestId = payoutRequest.Id,
                RemainingBalance = profile.CurrentEarnings
            };
        }

        public async Task<List<MechanicPayoutRequest>> GetPayoutRequestsForMechanicAsync(int mechanicId)
        {
            return await _dbContext.MechanicPayoutRequests
                .Where(r => r.MechanicId == mechanicId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<PayoutRequestViewModel>> GetAllPayoutRequestsAsync()
        {
            var requests = await _dbContext.MechanicPayoutRequests
                .Include(r => r.Mechanic)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var list = new List<PayoutRequestViewModel>();
            foreach (var req in requests)
            {
                var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == req.MechanicId);
                list.Add(new PayoutRequestViewModel
                {
                    Request = req,
                    MechanicName = req.Mechanic?.Name ?? "Mechanic",
                    PhoneNumber = req.Mechanic?.PhoneNumber ?? "",
                    DisplayId = req.Mechanic?.DisplayId ?? "",
                    City = profile?.City ?? "Noida"
                });
            }

            return list;
        }

        public async Task<bool> ProcessPayoutRequestAsync(AdminProcessPayoutDto dto)
        {
            var payout = await _dbContext.MechanicPayoutRequests.FindAsync(dto.PayoutRequestId);
            if (payout == null || payout.Status != "Pending") return false;

            if (dto.Action == "Approve")
            {
                payout.Status = "Approved";
                payout.ProcessedAt = DateTime.UtcNow;
                payout.AdminRemarks = string.IsNullOrWhiteSpace(dto.Remarks) ? "Payout Approved and Transferred" : dto.Remarks;
                payout.TransactionReference = string.IsNullOrWhiteSpace(dto.TransactionReference) ? "TXN" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper() : dto.TransactionReference;
            }
            else if (dto.Action == "Reject")
            {
                payout.Status = "Rejected";
                payout.ProcessedAt = DateTime.UtcNow;
                payout.AdminRemarks = string.IsNullOrWhiteSpace(dto.Remarks) ? "Payout Rejected" : dto.Remarks;

                // Refund the amount back to mechanic wallet
                var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == payout.MechanicId);
                if (profile != null)
                {
                    profile.CurrentEarnings += payout.Amount;
                }
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
