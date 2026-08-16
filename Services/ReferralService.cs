using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public class ReferralService : IReferralService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static bool _schemaEnsured = false;

        public ReferralService(ApplicationDbContext dbContext, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        private async Task EnsureSchemaAsync()
        {
            if (_schemaEnsured) return;
            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[ReferralProgramSettings]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [ReferralProgramSettings] (
                            [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [IsMasterEnabled] bit NOT NULL DEFAULT 1,
                            [M2M_Enabled] bit NOT NULL DEFAULT 1,
                            [M2M_ReferrerReward] float NOT NULL DEFAULT 300.0,
                            [M2M_RefereeReward] float NOT NULL DEFAULT 150.0,
                            [M2C_Enabled] bit NOT NULL DEFAULT 1,
                            [M2C_ReferrerReward] float NOT NULL DEFAULT 150.0,
                            [M2C_RefereeReward] float NOT NULL DEFAULT 100.0,
                            [C2C_Enabled] bit NOT NULL DEFAULT 1,
                            [C2C_ReferrerReward] float NOT NULL DEFAULT 100.0,
                            [C2C_RefereeReward] float NOT NULL DEFAULT 50.0,
                            [C2M_Enabled] bit NOT NULL DEFAULT 1,
                            [C2M_ReferrerReward] float NOT NULL DEFAULT 250.0,
                            [C2M_RefereeReward] float NOT NULL DEFAULT 100.0,
                            [MinWithdrawalAmount] float NOT NULL DEFAULT 100.0,
                            [MinJobAmountForReward] float NOT NULL DEFAULT 150.0,
                            [UpdatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                        );
                    END
                    ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ReferralProgramSettings]') AND name = N'M2M_Enabled')
                    BEGIN
                        DROP TABLE [ReferralProgramSettings];
                        CREATE TABLE [ReferralProgramSettings] (
                            [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [IsMasterEnabled] bit NOT NULL DEFAULT 1,
                            [M2M_Enabled] bit NOT NULL DEFAULT 1,
                            [M2M_ReferrerReward] float NOT NULL DEFAULT 300.0,
                            [M2M_RefereeReward] float NOT NULL DEFAULT 150.0,
                            [M2C_Enabled] bit NOT NULL DEFAULT 1,
                            [M2C_ReferrerReward] float NOT NULL DEFAULT 150.0,
                            [M2C_RefereeReward] float NOT NULL DEFAULT 100.0,
                            [C2C_Enabled] bit NOT NULL DEFAULT 1,
                            [C2C_ReferrerReward] float NOT NULL DEFAULT 100.0,
                            [C2C_RefereeReward] float NOT NULL DEFAULT 50.0,
                            [C2M_Enabled] bit NOT NULL DEFAULT 1,
                            [C2M_ReferrerReward] float NOT NULL DEFAULT 250.0,
                            [C2M_RefereeReward] float NOT NULL DEFAULT 100.0,
                            [MinWithdrawalAmount] float NOT NULL DEFAULT 100.0,
                            [MinJobAmountForReward] float NOT NULL DEFAULT 150.0,
                            [UpdatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                        );
                    END;

                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[ReferralTransactions]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [ReferralTransactions] (
                            [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [ReferrerUserId] int NOT NULL,
                            [RefereeUserId] int NOT NULL,
                            [StageType] nvarchar(20) NOT NULL DEFAULT 'C2C',
                            [ReferralCodeUsed] nvarchar(50) NOT NULL DEFAULT '',
                            [ReferrerRewardAmount] float NOT NULL DEFAULT 0.0,
                            [RefereeRewardAmount] float NOT NULL DEFAULT 0.0,
                            [Status] nvarchar(30) NOT NULL DEFAULT 'Pending',
                            [TriggerJobId] int NULL,
                            [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                            [CompletedAt] datetime2 NULL,
                            [Remarks] nvarchar(255) NOT NULL DEFAULT ''
                        );
                    END
                    ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ReferralTransactions]') AND name = N'RefereeUserId')
                    BEGIN
                        DROP TABLE [ReferralTransactions];
                        CREATE TABLE [ReferralTransactions] (
                            [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [ReferrerUserId] int NOT NULL,
                            [RefereeUserId] int NOT NULL,
                            [StageType] nvarchar(20) NOT NULL DEFAULT 'C2C',
                            [ReferralCodeUsed] nvarchar(50) NOT NULL DEFAULT '',
                            [ReferrerRewardAmount] float NOT NULL DEFAULT 0.0,
                            [RefereeRewardAmount] float NOT NULL DEFAULT 0.0,
                            [Status] nvarchar(30) NOT NULL DEFAULT 'Pending',
                            [TriggerJobId] int NULL,
                            [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                            [CompletedAt] datetime2 NULL,
                            [Remarks] nvarchar(255) NOT NULL DEFAULT ''
                        );
                    END;

                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[ReferralWithdrawalRequests]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [ReferralWithdrawalRequests] (
                            [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [UserId] int NOT NULL,
                            [UserRole] nvarchar(20) NOT NULL DEFAULT 'Customer',
                            [Amount] float NOT NULL,
                            [PayoutMethod] nvarchar(50) NOT NULL DEFAULT 'UPI',
                            [AccountHolderName] nvarchar(200) NOT NULL DEFAULT '',
                            [BankAccountNumber] nvarchar(100) NOT NULL DEFAULT '',
                            [BankName] nvarchar(200) NOT NULL DEFAULT '',
                            [IfscCode] nvarchar(50) NOT NULL DEFAULT '',
                            [UpiId] nvarchar(100) NOT NULL DEFAULT '',
                            [Status] nvarchar(50) NOT NULL DEFAULT 'Pending',
                            [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                            [ProcessedAt] datetime2 NULL,
                            [AdminRemarks] nvarchar(500) NOT NULL DEFAULT '',
                            [TransactionReference] nvarchar(100) NOT NULL DEFAULT ''
                        );
                    END
                    ELSE IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ReferralWithdrawalRequests]') AND name = N'PayoutMethod')
                    BEGIN
                        DROP TABLE [ReferralWithdrawalRequests];
                        CREATE TABLE [ReferralWithdrawalRequests] (
                            [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [UserId] int NOT NULL,
                            [UserRole] nvarchar(20) NOT NULL DEFAULT 'Customer',
                            [Amount] float NOT NULL,
                            [PayoutMethod] nvarchar(50) NOT NULL DEFAULT 'UPI',
                            [AccountHolderName] nvarchar(200) NOT NULL DEFAULT '',
                            [BankAccountNumber] nvarchar(100) NOT NULL DEFAULT '',
                            [BankName] nvarchar(200) NOT NULL DEFAULT '',
                            [IfscCode] nvarchar(50) NOT NULL DEFAULT '',
                            [UpiId] nvarchar(100) NOT NULL DEFAULT '',
                            [Status] nvarchar(50) NOT NULL DEFAULT 'Pending',
                            [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                            [ProcessedAt] datetime2 NULL,
                            [AdminRemarks] nvarchar(500) NOT NULL DEFAULT '',
                            [TransactionReference] nvarchar(100) NOT NULL DEFAULT ''
                        );
                    END;
                ");
                _schemaEnsured = true;
            }
            catch { }
        }

        public async Task<ReferralProgramSetting> GetSettingsAsync()
        {
            await EnsureSchemaAsync();
            var setting = await _dbContext.ReferralProgramSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new ReferralProgramSetting
                {
                    IsMasterEnabled = true,
                    M2M_Enabled = true,
                    M2M_ReferrerReward = 300.0,
                    M2M_RefereeReward = 150.0,
                    M2C_Enabled = true,
                    M2C_ReferrerReward = 150.0,
                    M2C_RefereeReward = 100.0,
                    C2C_Enabled = true,
                    C2C_ReferrerReward = 100.0,
                    C2C_RefereeReward = 50.0,
                    C2M_Enabled = true,
                    C2M_ReferrerReward = 250.0,
                    C2M_RefereeReward = 100.0,
                    MinWithdrawalAmount = 100.0,
                    MinJobAmountForReward = 150.0,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.ReferralProgramSettings.Add(setting);
                await _dbContext.SaveChangesAsync();
            }
            return setting;
        }

        public async Task<bool> UpdateSettingsAsync(ReferralProgramSetting updated)
        {
            var existing = await GetSettingsAsync();
            existing.IsMasterEnabled = updated.IsMasterEnabled;
            existing.M2M_Enabled = updated.M2M_Enabled;
            existing.M2M_ReferrerReward = updated.M2M_ReferrerReward;
            existing.M2M_RefereeReward = updated.M2M_RefereeReward;

            existing.M2C_Enabled = updated.M2C_Enabled;
            existing.M2C_ReferrerReward = updated.M2C_ReferrerReward;
            existing.M2C_RefereeReward = updated.M2C_RefereeReward;

            existing.C2C_Enabled = updated.C2C_Enabled;
            existing.C2C_ReferrerReward = updated.C2C_ReferrerReward;
            existing.C2C_RefereeReward = updated.C2C_RefereeReward;

            existing.C2M_Enabled = updated.C2M_Enabled;
            existing.C2M_ReferrerReward = updated.C2M_ReferrerReward;
            existing.C2M_RefereeReward = updated.C2M_RefereeReward;

            existing.MinWithdrawalAmount = updated.MinWithdrawalAmount;
            existing.MinJobAmountForReward = updated.MinJobAmountForReward;
            existing.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<string> EnsureUserReferralCodeAsync(int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return string.Empty;

            if (!string.IsNullOrWhiteSpace(user.ReferralCode))
            {
                return user.ReferralCode;
            }

            // Generate clean unique code, e.g., RS-AMAN-8821 or RS-MECH-4910
            string prefix = user.Role == "Mechanic" ? "RSM" : "RSC";
            string cleanName = new string(user.Name.Where(char.IsLetterOrDigit).Take(4).ToArray()).ToUpper();
            if (string.IsNullOrWhiteSpace(cleanName)) cleanName = "USER";
            
            string code = $"{prefix}-{cleanName}-{user.Id:D3}";
            user.ReferralCode = code;
            await _dbContext.SaveChangesAsync();
            return code;
        }

        public async Task<bool> RegisterReferralSignupAsync(int refereeUserId, string referralCode)
        {
            if (string.IsNullOrWhiteSpace(referralCode)) return false;
            referralCode = referralCode.Trim().ToUpperInvariant();

            var settings = await GetSettingsAsync();
            if (!settings.IsMasterEnabled) return false;

            var referee = await _dbContext.Users.FindAsync(refereeUserId);
            if (referee == null) return false;

            // Find referrer by referral code or phone number
            var referrer = await _dbContext.Users.FirstOrDefaultAsync(u => u.ReferralCode == referralCode || (u.ReferralCode.ToUpper() == referralCode));
            if (referrer == null) return false;

            // Self-referral protection
            if (referrer.Id == referee.Id || referrer.PhoneNumber == referee.PhoneNumber)
            {
                return false;
            }

            // Determine Stage
            string stage = "C2C";
            double referrerReward = 0.0;
            double refereeReward = 0.0;
            bool isStageEnabled = false;

            if (referrer.Role == "Mechanic" && referee.Role == "Mechanic")
            {
                stage = "M2M";
                isStageEnabled = settings.M2M_Enabled;
                referrerReward = settings.M2M_ReferrerReward;
                refereeReward = settings.M2M_RefereeReward;
            }
            else if (referrer.Role == "Mechanic" && referee.Role == "Customer")
            {
                stage = "M2C";
                isStageEnabled = settings.M2C_Enabled;
                referrerReward = settings.M2C_ReferrerReward;
                refereeReward = settings.M2C_RefereeReward;
            }
            else if (referrer.Role == "Customer" && referee.Role == "Customer")
            {
                stage = "C2C";
                isStageEnabled = settings.C2C_Enabled;
                referrerReward = settings.C2C_ReferrerReward;
                refereeReward = settings.C2C_RefereeReward;
            }
            else if (referrer.Role == "Customer" && referee.Role == "Mechanic")
            {
                stage = "C2M";
                isStageEnabled = settings.C2M_Enabled;
                referrerReward = settings.C2M_ReferrerReward;
                refereeReward = settings.C2M_RefereeReward;
            }

            if (!isStageEnabled) return false;

            referee.ReferredByCode = referrer.ReferralCode;

            // Check if transaction already exists
            var existingTx = await _dbContext.ReferralTransactions.FirstOrDefaultAsync(t => t.RefereeUserId == referee.Id);
            if (existingTx == null)
            {
                var transaction = new ReferralTransaction
                {
                    ReferrerUserId = referrer.Id,
                    RefereeUserId = referee.Id,
                    StageType = stage,
                    ReferralCodeUsed = referrer.ReferralCode,
                    ReferrerRewardAmount = referrerReward,
                    RefereeRewardAmount = refereeReward,
                    Status = "Pending",
                    CreatedAt = DateTime.UtcNow,
                    Remarks = $"Referral registered ({stage}) via code {referrer.ReferralCode}. Awaiting first successful service/job."
                };
                _dbContext.ReferralTransactions.Add(transaction);
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ProcessJobCompletionReferralRewardAsync(int jobId)
        {
            var job = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null || job.Status != "Completed") return false;

            var settings = await GetSettingsAsync();
            if (!settings.IsMasterEnabled) return false;

            // Check minimum job bill condition
            if (job.FinalBillAmount < settings.MinJobAmountForReward)
            {
                return false;
            }

            bool anyRewarded = false;

            // 1. Check Customer Referee (C2C or M2C pending referral)
            var customerTx = await _dbContext.ReferralTransactions
                .Include(t => t.ReferrerUser)
                .Include(t => t.RefereeUser)
                .FirstOrDefaultAsync(t => t.RefereeUserId == job.CustomerId && t.Status == "Pending");

            if (customerTx != null)
            {
                // Verify stage is enabled
                bool enabled = customerTx.StageType switch
                {
                    "C2C" => settings.C2C_Enabled,
                    "M2C" => settings.M2C_Enabled,
                    _ => true
                };

                if (enabled && customerTx.ReferrerUser != null && customerTx.RefereeUser != null)
                {
                    // Credit Referrer
                    customerTx.ReferrerUser.ReferralWalletBalance += customerTx.ReferrerRewardAmount;
                    
                    // Credit Referee
                    customerTx.RefereeUser.ReferralWalletBalance += customerTx.RefereeRewardAmount;

                    customerTx.Status = "Completed";
                    customerTx.TriggerJobId = job.Id;
                    customerTx.CompletedAt = DateTime.UtcNow;
                    customerTx.Remarks = $"Reward credited for Job #{job.Id}. Referrer: +₹{customerTx.ReferrerRewardAmount}, Referee: +₹{customerTx.RefereeRewardAmount}";

                    anyRewarded = true;
                }
            }

            // 2. Check Mechanic Referee (M2M or C2M pending referral)
            if (job.MechanicId.HasValue)
            {
                var mechanicTx = await _dbContext.ReferralTransactions
                    .Include(t => t.ReferrerUser)
                    .Include(t => t.RefereeUser)
                    .FirstOrDefaultAsync(t => t.RefereeUserId == job.MechanicId.Value && t.Status == "Pending");

                if (mechanicTx != null)
                {
                    bool enabled = mechanicTx.StageType switch
                    {
                        "M2M" => settings.M2M_Enabled,
                        "C2M" => settings.C2M_Enabled,
                        _ => true
                    };

                    if (enabled && mechanicTx.ReferrerUser != null && mechanicTx.RefereeUser != null)
                    {
                        // Credit Referrer
                        mechanicTx.ReferrerUser.ReferralWalletBalance += mechanicTx.ReferrerRewardAmount;

                        // Credit Referee
                        mechanicTx.RefereeUser.ReferralWalletBalance += mechanicTx.RefereeRewardAmount;

                        mechanicTx.Status = "Completed";
                        mechanicTx.TriggerJobId = job.Id;
                        mechanicTx.CompletedAt = DateTime.UtcNow;
                        mechanicTx.Remarks = $"Reward credited for Mechanic's completed Job #{job.Id}. Referrer: +₹{mechanicTx.ReferrerRewardAmount}, Referee: +₹{mechanicTx.RefereeRewardAmount}";

                        anyRewarded = true;
                    }
                }
            }

            if (anyRewarded)
            {
                await _dbContext.SaveChangesAsync();
            }

            return anyRewarded;
        }

        public async Task<ReferralDashboardSummaryDto> GetUserReferralSummaryAsync(int userId)
        {
            await EnsureSchemaAsync();
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return new ReferralDashboardSummaryDto();

            string code = await EnsureUserReferralCodeAsync(userId);

            var host = _httpContextAccessor?.HttpContext?.Request?.Host.Value ?? "raahsathi.com";
            var scheme = _httpContextAccessor?.HttpContext?.Request?.Scheme ?? "https";
            string shareLink = $"{scheme}://{host}/Auth/Login?ref={code}";

            var referrals = await _dbContext.ReferralTransactions
                .Include(t => t.RefereeUser)
                .Where(t => t.ReferrerUserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            double pendingRewards = referrals.Where(r => r.Status == "Pending").Sum(r => r.ReferrerRewardAmount);
            double completedRewards = referrals.Where(r => r.Status == "Completed").Sum(r => r.ReferrerRewardAmount);

            var friendList = referrals.Select(r => new ReferralFriendItemDto
            {
                Id = r.Id,
                FriendName = r.RefereeUser?.Name ?? "Referred User",
                FriendPhone = MaskPhone(r.RefereeUser?.PhoneNumber ?? ""),
                FriendRole = r.RefereeUser?.Role ?? "Customer",
                StageType = r.StageType,
                ExpectedReward = r.ReferrerRewardAmount,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                CompletedAt = r.CompletedAt
            }).ToList();

            var withdrawals = await _dbContext.ReferralWithdrawalRequests
                .Where(w => w.UserId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .ToListAsync();

            return new ReferralDashboardSummaryDto
            {
                UserId = userId,
                ReferralCode = code,
                ShareLink = shareLink,
                ReferralWalletBalance = Math.Round(user.ReferralWalletBalance, 2),
                LifetimeEarnings = Math.Round(completedRewards, 2),
                PendingRewardAmount = Math.Round(pendingRewards, 2),
                TotalReferredCount = referrals.Count,
                SuccessfulReferralCount = referrals.Count(r => r.Status == "Completed"),
                PendingReferralCount = referrals.Count(r => r.Status == "Pending"),
                ReferredFriends = friendList,
                WithdrawalHistory = withdrawals,
                Settings = await GetSettingsAsync()
            };
        }

        private static string MaskPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 6) return phone;
            return phone.Substring(0, 2) + "******" + phone.Substring(phone.Length - 2);
        }

        public async Task<ReferralWithdrawalResultDto> RequestReferralWithdrawalAsync(int userId, double amount, string payoutMethod, string accountHolder, string bankAccount, string bankName, string ifsc, string upiId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
            {
                return new ReferralWithdrawalResultDto { Success = false, Message = "User not found." };
            }

            var settings = await GetSettingsAsync();
            if (amount < settings.MinWithdrawalAmount)
            {
                return new ReferralWithdrawalResultDto { Success = false, Message = $"Minimum withdrawal amount is ₹{settings.MinWithdrawalAmount:N0}." };
            }

            if (user.ReferralWalletBalance < amount)
            {
                return new ReferralWithdrawalResultDto
                {
                    Success = false,
                    Message = $"Insufficient referral balance. Available: ₹{user.ReferralWalletBalance:F2}, Requested: ₹{amount:F2}"
                };
            }

            // Deduct from referral wallet
            user.ReferralWalletBalance -= amount;

            var request = new ReferralWithdrawalRequest
            {
                UserId = userId,
                UserRole = user.Role,
                Amount = amount,
                PayoutMethod = string.IsNullOrWhiteSpace(payoutMethod) ? "UPI" : payoutMethod,
                AccountHolderName = accountHolder ?? user.Name,
                BankAccountNumber = bankAccount ?? string.Empty,
                BankName = bankName ?? string.Empty,
                IfscCode = ifsc ?? string.Empty,
                UpiId = upiId ?? string.Empty,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.ReferralWithdrawalRequests.Add(request);
            await _dbContext.SaveChangesAsync();

            return new ReferralWithdrawalResultDto
            {
                Success = true,
                Message = "Withdrawal request submitted successfully! Admin will process payout shortly.",
                RequestId = request.Id,
                RemainingBalance = user.ReferralWalletBalance
            };
        }

        public async Task<List<ReferralWithdrawalRequest>> GetPendingWithdrawalRequestsAsync()
        {
            await EnsureSchemaAsync();
            return await _dbContext.ReferralWithdrawalRequests
                .Include(r => r.User)
                .Where(r => r.Status == "Pending")
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<ReferralWithdrawalRequest>> GetAllWithdrawalRequestsAsync()
        {
            await EnsureSchemaAsync();
            return await _dbContext.ReferralWithdrawalRequests
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> ProcessWithdrawalApprovalAsync(int requestId, string transactionRef, string remarks)
        {
            var req = await _dbContext.ReferralWithdrawalRequests.FindAsync(requestId);
            if (req == null || req.Status != "Pending") return false;

            req.Status = "Approved";
            req.ProcessedAt = DateTime.UtcNow;
            req.TransactionReference = string.IsNullOrWhiteSpace(transactionRef) ? "REF-PAY-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper() : transactionRef;
            req.AdminRemarks = string.IsNullOrWhiteSpace(remarks) ? "Referral reward payout processed by Admin" : remarks;

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ProcessWithdrawalRejectionAsync(int requestId, string remarks)
        {
            var req = await _dbContext.ReferralWithdrawalRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (req == null || req.Status != "Pending") return false;

            req.Status = "Rejected";
            req.ProcessedAt = DateTime.UtcNow;
            req.AdminRemarks = string.IsNullOrWhiteSpace(remarks) ? "Request rejected by Admin" : remarks;

            // Refund balance back to user's ReferralWalletBalance
            if (req.User != null)
            {
                req.User.ReferralWalletBalance += req.Amount;
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
