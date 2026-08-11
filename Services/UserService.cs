using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public class UserService : IUserService
    {
        private readonly ApplicationDbContext _dbContext;

        public UserService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
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
                var mechProfile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(m => m.UserId == userId);
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
            var user = await _dbContext.Users.FindAsync(dto.UserId);
            if (user == null) return false;

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                user.Name = dto.Name.Trim();
            }

            if (user.Role == "Mechanic")
            {
                var mechProfile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(m => m.UserId == dto.UserId);
                if (mechProfile != null)
                {
                    if (!string.IsNullOrWhiteSpace(dto.ShopName)) mechProfile.ShopName = dto.ShopName.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.ShopAddress)) mechProfile.ShopAddress = dto.ShopAddress.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.City)) mechProfile.City = dto.City.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.VehicleExpertise)) mechProfile.VehicleExpertise = dto.VehicleExpertise.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.Specialization)) mechProfile.Specialization = dto.Specialization.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.BankName)) mechProfile.BankName = dto.BankName.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.BankAccountNumber)) mechProfile.BankAccountNumber = dto.BankAccountNumber.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.IfscCode)) mechProfile.IfscCode = dto.IfscCode.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.UpiId)) mechProfile.UpiId = dto.UpiId.Trim();
                    if (!string.IsNullOrWhiteSpace(dto.AccountHolderName)) mechProfile.AccountHolderName = dto.AccountHolderName.Trim();
                }
            }

            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<List<Vehicle>> GetUserVehiclesAsync(int customerId)
        {
            return await _dbContext.Vehicles
                .Where(v => v.UserId == customerId)
                .ToListAsync();
        }

        public async Task<Vehicle?> AddVehicleAsync(int customerId, Vehicle vehicle)
        {
            vehicle.UserId = customerId;
            _dbContext.Vehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();
            return vehicle;
        }

        public async Task<MechanicProfile?> GetMechanicProfileAsync(int userId)
        {
            return await _dbContext.MechanicProfiles.FirstOrDefaultAsync(m => m.UserId == userId);
        }

        public async Task<bool> UpdateMechanicOnlineStatusAsync(int userId, bool isOnline)
        {
            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(m => m.UserId == userId);
            if (profile == null) return false;

            profile.IsOnline = isOnline;
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
