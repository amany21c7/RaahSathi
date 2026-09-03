using System.Threading.Tasks;

namespace RaahSathi.Services
{
    public interface IWhatsAppOtpService
    {
        Task<(bool Success, string Message, string? DevOtp)> SendOtpAsync(string phoneNumber, string purpose);
        (bool Success, string Message) VerifyOtp(string phoneNumber, string otp);
        bool IsPhoneVerified(string phoneNumber);
        void ClearPhoneVerification(string phoneNumber);
        Task<(bool IsConnected, string? QrDataUrl, string? ConnectedPhone, string Message)> GetGatewayStatusAsync();
        Task<(bool Success, string Message)> LogoutGatewayAsync();
    }
}
