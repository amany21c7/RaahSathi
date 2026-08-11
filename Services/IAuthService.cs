using System.Threading.Tasks;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto> AuthenticateAsync(LoginRequestDto request);
        Task<AuthResponseDto> RegisterUserAsync(RegisterRequestDto request);
        Task<AuthResponseDto> SendOtpAsync(SendOtpRequestDto request);
        Task<AuthResponseDto> VerifyOtpAsync(VerifyOtpRequestDto request);
        Task<User?> GetUserByIdAsync(int userId);
        Task<User?> GetUserByPhoneAndRoleAsync(string phoneNumber, string role);
    }
}
