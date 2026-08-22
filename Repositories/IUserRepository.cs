using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Repositories
{
    public interface IUserRepository
    {
        Task<User?> GetUserByIdAsync(int userId);
        Task<MechanicProfile?> GetMechanicProfileAsync(int userId);
        Task<bool> UpdateUserProfileViaStoredProcedureAsync(UpdateProfileRequestDto dto);
        Task<List<Vehicle>> GetUserVehiclesAsync(int customerId);
        Task<Vehicle> AddVehicleAsync(int customerId, Vehicle vehicle);
        Task<bool> UpdateMechanicOnlineStatusAsync(int userId, bool isOnline);
    }
}
