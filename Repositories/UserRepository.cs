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
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public UserRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            try
            {
                return await _dbContext.Users.FindAsync(userId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<MechanicProfile?> GetMechanicProfileAsync(int userId)
        {
            try
            {
                return await _dbContext.MechanicProfiles.FirstOrDefaultAsync(m => m.UserId == userId);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<bool> UpdateUserProfileViaStoredProcedureAsync(UpdateProfileRequestDto dto)
        {
            try
            {
                if (_dbContext.Database.IsSqlServer())
                {
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        "EXEC dbo.rs_users_update_profile @UserId = {0}, @Name = {1}, @ShopName = {2}, @ShopAddress = {3}, @City = {4}, @VehicleExpertise = {5}, @Specialization = {6}, @BankName = {7}, @BankAccountNumber = {8}, @IfscCode = {9}, @UpiId = {10}, @AccountHolderName = {11}",
                        dto.UserId,
                        dto.Name ?? string.Empty,
                        dto.ShopName ?? string.Empty,
                        dto.ShopAddress ?? string.Empty,
                        dto.City ?? string.Empty,
                        dto.VehicleExpertise ?? string.Empty,
                        dto.Specialization ?? string.Empty,
                        dto.BankName ?? string.Empty,
                        dto.BankAccountNumber ?? string.Empty,
                        dto.IfscCode ?? string.Empty,
                        dto.UpiId ?? string.Empty,
                        dto.AccountHolderName ?? string.Empty
                    );
                    return true;
                }
            }
            catch (Exception) { }

            // Fallback C# atomic transaction
            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
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

        public async Task<List<Vehicle>> GetUserVehiclesAsync(int customerId)
        {
            try
            {
                return await _dbContext.Vehicles
                    .Where(v => v.UserId == customerId)
                    .OrderByDescending(v => v.Id)
                    .ToListAsync();
            }
            catch (Exception)
            {
                return new List<Vehicle>();
            }
        }

        public async Task<Vehicle> AddVehicleAsync(int customerId, Vehicle vehicle)
        {
            try
            {
                vehicle.UserId = customerId;
                _dbContext.Vehicles.Add(vehicle);
                await _dbContext.SaveChangesAsync();
                return vehicle;
            }
            catch (Exception)
            {
                return vehicle;
            }
        }

        public async Task<bool> UpdateMechanicOnlineStatusAsync(int userId, bool isOnline)
        {
            try
            {
                var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile == null) return false;

                profile.IsOnline = isOnline;
                await _dbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
