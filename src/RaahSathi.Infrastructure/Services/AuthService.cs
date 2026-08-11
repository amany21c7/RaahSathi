using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _dbContext;

        public AuthService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private static string CleanPhoneNumber(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91"))
            {
                digits = digits.Substring(2);
            }
            return digits;
        }

        public async Task<AuthResponseDto> AuthenticateAsync(LoginRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Password))
            {
                return new AuthResponseDto { Success = false, Message = "Phone number and password are required." };
            }

            string cleanPhone = CleanPhoneNumber(request.PhoneNumber);
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == request.Role);

            if (user == null)
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone));
            }

            if (user == null)
            {
                return new AuthResponseDto { Success = false, Message = "Account not found for the given mobile number." };
            }

            if (user.IsBlocked)
            {
                return new AuthResponseDto { Success = false, Message = "Account has been suspended or blocked by Admin." };
            }

            bool isPasswordValid = PasswordHasher.VerifyPassword(user.Password, request.Password) || user.Password == request.Password;
            if (!isPasswordValid)
            {
                return new AuthResponseDto { Success = false, Message = "Invalid password." };
            }

            return new AuthResponseDto
            {
                Success = true,
                Message = "Login successful.",
                UserId = user.Id,
                DisplayId = user.DisplayId,
                Name = user.Name,
                Role = user.Role,
                Token = "JWT_SIMULATED_TOKEN_" + Guid.NewGuid().ToString("N")
            };
        }

        public async Task<AuthResponseDto> RegisterUserAsync(RegisterRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.PhoneNumber) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Password))
            {
                return new AuthResponseDto { Success = false, Message = "Name, Phone number and Password are required." };
            }

            string cleanPhone = CleanPhoneNumber(request.PhoneNumber);
            string role = string.IsNullOrWhiteSpace(request.Role) ? "Customer" : request.Role;

            var existingUser = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == role);
            if (existingUser != null)
            {
                if (!string.IsNullOrWhiteSpace(existingUser.Password) && existingUser.Password != "OTP_USER_123")
                {
                    return new AuthResponseDto { Success = false, Message = "An account with this phone number already exists for this role." };
                }

                // Update unpassworded account
                existingUser.Name = request.Name.Trim();
                existingUser.Password = PasswordHasher.HashPassword(request.Password);
                await _dbContext.SaveChangesAsync();

                return new AuthResponseDto
                {
                    Success = true,
                    Message = "Account updated and registered successfully.",
                    UserId = existingUser.Id,
                    DisplayId = existingUser.DisplayId,
                    Name = existingUser.Name,
                    Role = existingUser.Role,
                    Token = "JWT_SIMULATED_TOKEN_" + Guid.NewGuid().ToString("N")
                };
            }

            var newUser = new User
            {
                Name = request.Name.Trim(),
                PhoneNumber = cleanPhone,
                Role = role,
                Password = PasswordHasher.HashPassword(request.Password),
                CreatedAt = DateTime.UtcNow,
                IsBlocked = false,
                AdminRole = role == "Admin" ? "Super Admin" : ""
            };

            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();

            if (role == "Mechanic")
            {
                var mechanicProfile = new MechanicProfile
                {
                    UserId = newUser.Id,
                    IsOnline = true,
                    KycStatus = "Approved",
                    Rating = 5.0,
                    TotalJobs = 0,
                    CommissionRate = 0.20,
                    CurrentEarnings = 500.0, // Sign up bonus / default balance
                    ShopName = request.Name.Trim() + " Garage",
                    City = string.IsNullOrWhiteSpace(request.City) ? "Noida" : request.City,
                    VehicleExpertise = "Bike, Car",
                    Specialization = "General Service, Breakdown Assist"
                };

                _dbContext.MechanicProfiles.Add(mechanicProfile);
                await _dbContext.SaveChangesAsync();
            }

            return new AuthResponseDto
            {
                Success = true,
                Message = "Registration successful.",
                UserId = newUser.Id,
                DisplayId = newUser.DisplayId,
                Name = newUser.Name,
                Role = newUser.Role,
                Token = "JWT_SIMULATED_TOKEN_" + Guid.NewGuid().ToString("N")
            };
        }

        public async Task<AuthResponseDto> SendOtpAsync(SendOtpRequestDto request)
        {
            string cleanPhone = CleanPhoneNumber(request.PhoneNumber);
            if (string.IsNullOrWhiteSpace(cleanPhone) || cleanPhone.Length < 10)
            {
                return new AuthResponseDto { Success = false, Message = "Invalid phone number." };
            }

            string role = string.IsNullOrWhiteSpace(request.Role) ? "Customer" : request.Role;

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == role);

            if (user == null)
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return new AuthResponseDto { Success = true, IsNewUser = true, Message = "New user prompt for name." };
                }

                // Register user via OTP flow
                return await RegisterUserAsync(new RegisterRequestDto
                {
                    Name = request.Name,
                    PhoneNumber = cleanPhone,
                    Password = "OTP_USER_123",
                    Role = role
                });
            }

            if (user.IsBlocked)
            {
                return new AuthResponseDto { Success = false, Message = "Account is suspended." };
            }

            return new AuthResponseDto
            {
                Success = true,
                Message = "OTP sent successfully (Simulated OTP: 1234)",
                UserId = user.Id,
                DisplayId = user.DisplayId,
                Name = user.Name,
                Role = user.Role,
                IsNewUser = false
            };
        }

        public async Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpRequestDto request)
        {
            // Simulated OTP check (accepts "1234" or "123456" in demo)
            if (string.IsNullOrWhiteSpace(request.Otp) || request.Otp.Length < 4)
            {
                return new AuthResponseDto { Success = false, Message = "Invalid OTP code." };
            }

            return await SendOtpAsync(new SendOtpRequestDto
            {
                PhoneNumber = request.PhoneNumber,
                Role = request.Role,
                Name = request.Name
            });
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _dbContext.Users.FindAsync(userId);
        }

        public async Task<User?> GetUserByPhoneAndRoleAsync(string phoneNumber, string role)
        {
            string cleanPhone = CleanPhoneNumber(phoneNumber);
            return await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == role);
        }
    }
}
