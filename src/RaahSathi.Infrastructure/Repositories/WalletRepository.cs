using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Repositories
{
    public class WalletRepository : IWalletRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public WalletRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<MechanicProfile?> GetMechanicProfileAsync(int mechanicId)
        {
            try
            {
                return await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == mechanicId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<double> GetPendingPayoutSumAsync(int mechanicId)
        {
            try
            {
                return await _dbContext.MechanicPayoutRequests
                    .Where(r => r.MechanicId == mechanicId && r.Status == "Pending")
                    .SumAsync(r => (double?)r.Amount) ?? 0.0;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        public async Task<double> GetApprovedPayoutSumAsync(int mechanicId)
        {
            try
            {
                return await _dbContext.MechanicPayoutRequests
                    .Where(r => r.MechanicId == mechanicId && r.Status == "Approved")
                    .SumAsync(r => (double?)r.Amount) ?? 0.0;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        public async Task<PayoutResponseDto> RequestPayoutViaStoredProcedureAsync(CreatePayoutRequestDto request, MechanicProfile profile)
        {
            try
            {
                if (_dbContext.Database.IsSqlServer())
                {
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        "EXEC dbo.sp_RequestMechanicPayout @MechanicId = {0}, @Amount = {1}, @PayoutMethod = {2}, @AccountHolderName = {3}, @BankAccountNumber = {4}, @BankName = {5}, @IfscCode = {6}, @UpiId = {7}",
                        request.MechanicId,
                        request.Amount,
                        string.IsNullOrWhiteSpace(request.PayoutMethod) ? "UPI" : request.PayoutMethod,
                        request.AccountHolderName ?? profile.AccountHolderName,
                        request.BankAccountNumber ?? profile.BankAccountNumber,
                        request.BankName ?? profile.BankName,
                        request.IfscCode ?? profile.IfscCode,
                        request.UpiId ?? profile.UpiId
                    );

                    var updatedProfile = await _dbContext.MechanicProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == request.MechanicId);
                    var latestReq = await _dbContext.MechanicPayoutRequests.Where(r => r.MechanicId == request.MechanicId).OrderByDescending(r => r.Id).FirstOrDefaultAsync();

                    return new PayoutResponseDto
                    {
                        Success = true,
                        Message = "Payout request submitted successfully via secure transaction. Admin review pending.",
                        PayoutRequestId = latestReq?.Id ?? 0,
                        RemainingBalance = updatedProfile?.CurrentEarnings ?? 0.0
                    };
                }
            }
            catch (Exception) { }

            // Fallback C# transaction
            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    var liveProfile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == request.MechanicId);
                    if (liveProfile == null || liveProfile.CurrentEarnings < request.Amount)
                    {
                        await transaction.RollbackAsync();
                        return new PayoutResponseDto
                        {
                            Success = false,
                            Message = $"Insufficient wallet balance. Available: ₹{liveProfile?.CurrentEarnings ?? 0:F2}"
                        };
                    }

                    liveProfile.CurrentEarnings -= request.Amount;

                    var payoutRequest = new MechanicPayoutRequest
                    {
                        MechanicId = request.MechanicId,
                        Amount = request.Amount,
                        PayoutMethod = string.IsNullOrWhiteSpace(request.PayoutMethod) ? "UPI" : request.PayoutMethod,
                        AccountHolderName = request.AccountHolderName ?? liveProfile.AccountHolderName,
                        BankAccountNumber = request.BankAccountNumber ?? liveProfile.BankAccountNumber,
                        BankName = request.BankName ?? liveProfile.BankName,
                        IfscCode = request.IfscCode ?? liveProfile.IfscCode,
                        UpiId = request.UpiId ?? liveProfile.UpiId,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.MechanicPayoutRequests.Add(payoutRequest);
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new PayoutResponseDto
                    {
                        Success = true,
                        Message = "Payout request submitted successfully. Admin review pending.",
                        PayoutRequestId = payoutRequest.Id,
                        RemainingBalance = liveProfile.CurrentEarnings
                    };
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return new PayoutResponseDto { Success = false, Message = "Transaction error: " + ex.Message };
                }
            }
            catch (Exception ex)
            {
                return new PayoutResponseDto { Success = false, Message = "Could not process request: " + ex.Message };
            }
        }

        public async Task<List<MechanicPayoutRequest>> GetPayoutRequestsForMechanicAsync(int mechanicId)
        {
            try
            {
                return await _dbContext.MechanicPayoutRequests
                    .Where(r => r.MechanicId == mechanicId)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<MechanicPayoutRequest>();
            }
        }

        public async Task<List<MechanicPayoutRequest>> GetAllPayoutRequestsAsync()
        {
            try
            {
                return await _dbContext.MechanicPayoutRequests
                    .Include(r => r.Mechanic)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<MechanicPayoutRequest>();
            }
        }

        public async Task<bool> ProcessPayoutViaStoredProcedureAsync(AdminProcessPayoutDto dto)
        {
            try
            {
                if (_dbContext.Database.IsSqlServer())
                {
                    string remarks = string.IsNullOrWhiteSpace(dto.Remarks) 
                        ? (dto.Action == "Approve" ? "Payout Approved and Transferred" : "Payout Rejected") 
                        : dto.Remarks;
                    string txnRef = string.IsNullOrWhiteSpace(dto.TransactionReference) 
                        ? "TXN" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper() 
                        : dto.TransactionReference;

                    await _dbContext.Database.ExecuteSqlRawAsync(
                        "EXEC dbo.sp_ProcessMechanicPayout @PayoutRequestId = {0}, @AdminAction = {1}, @AdminRemarks = {2}, @TransactionReference = {3}",
                        dto.PayoutRequestId,
                        dto.Action,
                        remarks,
                        txnRef
                    );
                    return true;
                }
            }
            catch (Exception) { }

            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
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

                        var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == payout.MechanicId);
                        if (profile != null)
                        {
                            profile.CurrentEarnings += payout.Amount;
                        }
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
