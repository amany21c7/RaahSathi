using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public interface IWalletService
    {
        Task<WalletBalanceDto> GetWalletBalanceAsync(int mechanicId);
        Task<PayoutResponseDto> RequestPayoutAsync(CreatePayoutRequestDto request);
        Task<List<MechanicPayoutRequest>> GetPayoutRequestsForMechanicAsync(int mechanicId);
        Task<List<PayoutRequestViewModel>> GetAllPayoutRequestsAsync();
        Task<bool> ProcessPayoutRequestAsync(AdminProcessPayoutDto dto);
    }
}
