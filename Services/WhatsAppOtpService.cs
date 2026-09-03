using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RaahSathi.Services
{
    public class WhatsAppOtpService : IWhatsAppOtpService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _config;
        private readonly ILogger<WhatsAppOtpService> _logger;

        private class OtpRecord
        {
            public string Otp { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public int Attempts { get; set; }
        }

        public WhatsAppOtpService(
            HttpClient httpClient,
            IMemoryCache cache,
            IConfiguration config,
            ILogger<WhatsAppOtpService> logger)
        {
            _httpClient = httpClient;
            _cache = cache;
            _config = config;
            _logger = logger;

            string baseUrl = _config["WhatsAppGateway:BaseUrl"] ?? "http://localhost:5005";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(8);
        }

        private static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91"))
            {
                digits = digits.Substring(2);
            }
            return digits;
        }

        public async Task<(bool Success, string Message, string? DevOtp)> SendOtpAsync(string phoneNumber, string purpose)
        {
            string cleanPhone = NormalizePhone(phoneNumber);
            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                return (false, "Please provide a valid 10-digit mobile number.", null);
            }

            string cooldownKey = $"OtpCooldown_{cleanPhone}";
            if (_cache.TryGetValue(cooldownKey, out DateTime cooldownUntil))
            {
                int remainingSeconds = Math.Max(1, (int)(cooldownUntil - DateTime.UtcNow).TotalSeconds);
                return (false, $"Please wait {remainingSeconds} seconds before requesting a new OTP.", null);
            }

            // Generate cryptographically secure 6-digit OTP
            string otp = RandomNumberGenerator.GetInt32(100000, 1000000).ToString("D6");

            int expiryMinutes = _config.GetValue<int>("WhatsAppGateway:OtpExpiryMinutes", 5);
            int cooldownSeconds = _config.GetValue<int>("WhatsAppGateway:CooldownSeconds", 60);
            bool fallbackDevOtp = _config.GetValue<bool>("WhatsAppGateway:FallbackDevOtp", true);

            // Store OTP in cache
            string otpKey = $"WhatsAppOtp_{cleanPhone}";
            var record = new OtpRecord
            {
                Otp = otp,
                CreatedAt = DateTime.UtcNow,
                Attempts = 0
            };
            _cache.Set(otpKey, record, TimeSpan.FromMinutes(expiryMinutes));
            _cache.Set(cooldownKey, DateTime.UtcNow.AddSeconds(cooldownSeconds), TimeSpan.FromSeconds(cooldownSeconds));

            // Format message
            string purposeText = purpose switch
            {
                "Registration" => "Account Registration",
                "Login" => "Account Login",
                "ForgotPassword" => "Password Reset",
                _ => "Verification"
            };

            string whatsappMessage = $"🔐 *RaahSathi {purposeText} OTP*\n\n" +
                                     $"Your 6-digit verification code is: *{otp}*\n\n" +
                                     $"⏱️ This code is valid for {expiryMinutes} minutes.\n" +
                                     $"⚠️ Never share this code with anyone.\n\n" +
                                     $"_RaahSathi - India's 24/7 Roadside Assistance Network_";

            // Attempt sending through WhatsApp Web QR Gateway
            try
            {
                var payload = new
                {
                    phone = cleanPhone,
                    otp = otp,
                    message = whatsappMessage
                };

                var response = await _httpClient.PostAsJsonAsync("send-otp", payload);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("WhatsApp OTP sent successfully to +91{Phone} for {Purpose}", cleanPhone, purpose);
                    return (true, $"OTP successfully sent to WhatsApp number +91 {cleanPhone}.", null);
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("WhatsApp Gateway responded with status {Status}: {Content}", response.StatusCode, errorContent);

                if (fallbackDevOtp)
                {
                    // Fallback to dev OTP if gateway is disconnected/waiting for QR scan
                    return (true, $"[Gateway not yet scanned] For testing, your WhatsApp OTP is: {otp}. Please scan QR in Admin Settings to enable automatic WhatsApp delivery.", otp);
                }

                return (false, "WhatsApp Gateway is currently waiting for QR scan. Please connect WhatsApp in Admin Settings.", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to communicate with WhatsApp Gateway at {BaseAddress}", _httpClient.BaseAddress);

                if (fallbackDevOtp)
                {
                    return (true, $"[Gateway Offline] For testing, your WhatsApp OTP is: {otp}. (Start whatsapp-gateway service to send real messages).", otp);
                }

                return (false, "Could not reach WhatsApp Gateway service. Please try again shortly.", null);
            }
        }

        public (bool Success, string Message) VerifyOtp(string phoneNumber, string otp)
        {
            string cleanPhone = NormalizePhone(phoneNumber);
            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                return (false, "Invalid phone number.");
            }

            if (string.IsNullOrWhiteSpace(otp))
            {
                return (false, "Please enter the 6-digit OTP received on WhatsApp.");
            }

            string cleanOtp = otp.Trim();
            string otpKey = $"WhatsAppOtp_{cleanPhone}";

            if (!_cache.TryGetValue(otpKey, out OtpRecord? record) || record == null)
            {
                return (false, "OTP has expired or was not requested. Please request a new OTP.");
            }

            record.Attempts++;
            if (record.Attempts > 5)
            {
                _cache.Remove(otpKey);
                return (false, "Too many incorrect attempts. This OTP is now invalid. Please request a new one.");
            }

            if (record.Otp != cleanOtp)
            {
                int remaining = 5 - record.Attempts;
                return (false, $"Invalid OTP code. {remaining} attempt(s) remaining.");
            }

            // OTP is valid!
            _cache.Remove(otpKey);

            // Set verified token valid for 10 minutes so user can complete registration / reset
            string verifiedKey = $"VerifiedPhone_{cleanPhone}";
            _cache.Set(verifiedKey, true, TimeSpan.FromMinutes(10));

            return (true, "WhatsApp number verified successfully!");
        }

        public bool IsPhoneVerified(string phoneNumber)
        {
            string cleanPhone = NormalizePhone(phoneNumber);
            if (string.IsNullOrEmpty(cleanPhone)) return false;
            return _cache.TryGetValue($"VerifiedPhone_{cleanPhone}", out bool verified) && verified;
        }

        public void ClearPhoneVerification(string phoneNumber)
        {
            string cleanPhone = NormalizePhone(phoneNumber);
            if (!string.IsNullOrEmpty(cleanPhone))
            {
                _cache.Remove($"VerifiedPhone_{cleanPhone}");
            }
        }

        public async Task<(bool IsConnected, string? QrDataUrl, string? ConnectedPhone, string Message)> GetGatewayStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("qr");
                if (response.IsSuccessStatusCode)
                {
                    using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                    var root = doc.RootElement;
                    bool isConnected = root.TryGetProperty("isConnected", out var connProp) && connProp.GetBoolean();
                    string? phone = root.TryGetProperty("connectedPhone", out var phoneProp) && phoneProp.ValueKind == JsonValueKind.String ? phoneProp.GetString() : null;
                    string? qr = root.TryGetProperty("qrDataUrl", out var qrProp) && qrProp.ValueKind == JsonValueKind.String ? qrProp.GetString() : null;

                    return (isConnected, qr, phone, isConnected ? "WhatsApp Connected" : "QR Code Ready to Scan");
                }

                return (false, null, null, $"Gateway returned HTTP {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error fetching WhatsApp Gateway status");
                return (false, null, null, "WhatsApp Gateway service is offline or unreachable.");
            }
        }

        public async Task<(bool Success, string Message)> LogoutGatewayAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync("logout", null);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "WhatsApp Gateway disconnected. Please scan new QR code.");
                }
                return (false, "Failed to disconnect WhatsApp Gateway.");
            }
            catch (Exception ex)
            {
                return (false, $"Error contacting WhatsApp Gateway: {ex.Message}");
            }
        }
    }
}
