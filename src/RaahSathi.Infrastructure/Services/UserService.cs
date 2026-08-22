using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.DTOs;
using RaahSathi.Models;
using RaahSathi.Repositories;

namespace RaahSathi.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
        {
            var user = await _userRepository.GetUserByIdAsync(userId);
            if (user == null) return null;

            var profileDto = new UserProfileDto
            {
                Id = user.Id,
                DisplayId = user.DisplayId,
                Name = user.Name,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            if (user.Role == "Mechanic")
            {
                var mechProfile = await _userRepository.GetMechanicProfileAsync(userId);
                if (mechProfile != null)
                {
                    profileDto.MechanicProfile = new MechanicProfileDto
                    {
                        IsOnline = mechProfile.IsOnline,
                        Rating = mechProfile.Rating,
                        TotalJobs = mechProfile.TotalJobs,
                        KycStatus = mechProfile.KycStatus,
                        ShopName = mechProfile.ShopName,
                        ShopAddress = mechProfile.ShopAddress,
                        City = mechProfile.City,
                        CurrentEarnings = mechProfile.CurrentEarnings,
                        VehicleExpertise = mechProfile.VehicleExpertise,
                        Specialization = mechProfile.Specialization,
                        ServiceRadiusKm = mechProfile.ServiceRadiusKm,
                        Languages = mechProfile.Languages,
                        BankName = mechProfile.BankName,
                        BankAccountNumber = mechProfile.BankAccountNumber,
                        IfscCode = mechProfile.IfscCode,
                        UpiId = mechProfile.UpiId,
                        AccountHolderName = mechProfile.AccountHolderName
                    };
                }
            }

            return profileDto;
        }

        public async Task<bool> UpdateUserProfileAsync(UpdateProfileRequestDto dto)
        {
            return await _userRepository.UpdateUserProfileViaStoredProcedureAsync(dto);
        }

        public async Task<List<Vehicle>> GetUserVehiclesAsync(int customerId)
        {
            return await _userRepository.GetUserVehiclesAsync(customerId);
        }

        public async Task<Vehicle?> AddVehicleAsync(int customerId, Vehicle vehicle)
        {
            return await _userRepository.AddVehicleAsync(customerId, vehicle);
        }

        public async Task<MechanicProfile?> GetMechanicProfileAsync(int userId)
        {
            return await _userRepository.GetMechanicProfileAsync(userId);
        }

        public async Task<bool> UpdateMechanicOnlineStatusAsync(int userId, bool isOnline)
        {
            return await _userRepository.UpdateMechanicOnlineStatusAsync(userId, isOnline);
        }
    }
}
