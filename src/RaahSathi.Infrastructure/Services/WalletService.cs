using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RaahSathi.DTOs;
using RaahSathi.Models;
using RaahSathi.Repositories;

namespace RaahSathi.Services
{
    public class WalletService : IWalletService
    {
        private readonly IWalletRepository _walletRepository;

        public WalletService(IWalletRepository walletRepository)
        {
            _walletRepository = walletRepository;
        }

        public async Task<WalletBalanceDto> GetWalletBalanceAsync(int mechanicId)
        {
            var profile = await _walletRepository.GetMechanicProfileAsync(mechanicId);
            double currentBalance = profile?.CurrentEarnings ?? 0.0;
            int totalJobs = profile?.TotalJobs ?? 0;

            double pendingPayouts = await _walletRepository.GetPendingPayoutSumAsync(mechanicId);
            double completedPayouts = await _walletRepository.GetApprovedPayoutSumAsync(mechanicId);

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

            var profile = await _walletRepository.GetMechanicProfileAsync(request.MechanicId);
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

            return await _walletRepository.RequestPayoutViaStoredProcedureAsync(request, profile);
        }

        public async Task<List<MechanicPayoutRequest>> GetPayoutRequestsForMechanicAsync(int mechanicId)
        {
            return await _walletRepository.GetPayoutRequestsForMechanicAsync(mechanicId);
        }

        public async Task<List<PayoutRequestViewModel>> GetAllPayoutRequestsAsync()
        {
            var requests = await _walletRepository.GetAllPayoutRequestsAsync();
            var list = new List<PayoutRequestViewModel>();

            foreach (var req in requests)
            {
                var profile = await _walletRepository.GetMechanicProfileAsync(req.MechanicId);
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
            return await _walletRepository.ProcessPayoutViaStoredProcedureAsync(dto);
        }
    }
}
