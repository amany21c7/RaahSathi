using System;
using System.ComponentModel.DataAnnotations;

namespace RaahSathi.DTOs
{
    public class LoginRequestDto
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "Customer"; // Customer, Mechanic, Admin
    }

    public class RegisterRequestDto
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "Customer"; // Customer, Mechanic

        public string? Email { get; set; }
        public string? City { get; set; }
    }

    public class SendOtpRequestDto
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;
        public string Role { get; set; } = "Customer";
        public string? Name { get; set; }
    }

    public class VerifyOtpRequestDto
    {
        [Required]
        public string PhoneNumber { get; set; } = string.Empty;
        [Required]
        public string Otp { get; set; } = string.Empty;
        public string Role { get; set; } = "Customer";
        public string? Name { get; set; }
    }

    public class AuthResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? UserId { get; set; }
        public string? DisplayId { get; set; }
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? Token { get; set; }
        public bool IsNewUser { get; set; }
    }
}
