using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public class ReferralFriendItemDto
    {
        public int Id { get; set; }
        public string FriendName { get; set; } = string.Empty;
        public string FriendPhone { get; set; } = string.Empty;
        public string FriendRole { get; set; } = string.Empty;
        public string StageType { get; set; } = string.Empty; // M2M, M2C, C2C, C2M
        public double ExpectedReward { get; set; }
        public string Status { get; set; } = "Pending"; // "Pending", "Completed"
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class ReferralDashboardSummaryDto
    {
        public int UserId { get; set; }
        public string ReferralCode { get; set; } = string.Empty;
        public string ShareLink { get; set; } = string.Empty;
        public double ReferralWalletBalance { get; set; }
        public double LifetimeEarnings { get; set; }
        public double PendingRewardAmount { get; set; }
        public int TotalReferredCount { get; set; }
        public int SuccessfulReferralCount { get; set; }
        public int PendingReferralCount { get; set; }
        public List<ReferralFriendItemDto> ReferredFriends { get; set; } = new List<ReferralFriendItemDto>();
        public List<ReferralWithdrawalRequest> WithdrawalHistory { get; set; } = new List<ReferralWithdrawalRequest>();
        public ReferralProgramSetting Settings { get; set; } = new ReferralProgramSetting();
    }

    public class ReferralWithdrawalResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public double RemainingBalance { get; set; }
        public int RequestId { get; set; }
    }

    public interface IReferralService
    {
        Task<ReferralProgramSetting> GetSettingsAsync();
        Task<bool> UpdateSettingsAsync(ReferralProgramSetting settings);
        Task<string> EnsureUserReferralCodeAsync(int userId);
        Task<bool> RegisterReferralSignupAsync(int refereeUserId, string referralCode);
        Task<bool> ProcessJobCompletionReferralRewardAsync(int jobId);
        Task<ReferralDashboardSummaryDto> GetUserReferralSummaryAsync(int userId);
        Task<ReferralWithdrawalResultDto> RequestReferralWithdrawalAsync(int userId, double amount, string payoutMethod, string accountHolder, string bankAccount, string bankName, string ifsc, string upiId);
        Task<List<ReferralWithdrawalRequest>> GetPendingWithdrawalRequestsAsync();
        Task<List<ReferralWithdrawalRequest>> GetAllWithdrawalRequestsAsync();
        Task<bool> ProcessWithdrawalApprovalAsync(int requestId, string transactionRef, string remarks);
        Task<bool> ProcessWithdrawalRejectionAsync(int requestId, string remarks);
    }
}
