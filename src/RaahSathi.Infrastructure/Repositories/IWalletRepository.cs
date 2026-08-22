using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Repositories
{
    public interface IWalletRepository
    {
        Task<MechanicProfile?> GetMechanicProfileAsync(int mechanicId);
        Task<double> GetPendingPayoutSumAsync(int mechanicId);
        Task<double> GetApprovedPayoutSumAsync(int mechanicId);
        Task<PayoutResponseDto> RequestPayoutViaStoredProcedureAsync(CreatePayoutRequestDto request, MechanicProfile profile);
        Task<List<MechanicPayoutRequest>> GetPayoutRequestsForMechanicAsync(int mechanicId);
        Task<List<MechanicPayoutRequest>> GetAllPayoutRequestsAsync();
        Task<bool> ProcessPayoutViaStoredProcedureAsync(AdminProcessPayoutDto dto);
    }
}
