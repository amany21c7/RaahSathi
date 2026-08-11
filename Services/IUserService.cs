using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public interface IUserService
    {
        Task<UserProfileDto?> GetUserProfileAsync(int userId);
        Task<bool> UpdateUserProfileAsync(UpdateProfileRequestDto dto);
        Task<List<Vehicle>> GetUserVehiclesAsync(int customerId);
        Task<Vehicle?> AddVehicleAsync(int customerId, Vehicle vehicle);
        Task<MechanicProfile?> GetMechanicProfileAsync(int userId);
        Task<bool> UpdateMechanicOnlineStatusAsync(int userId, bool isOnline);
    }
}
