using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RaahSathi.DTOs;
using RaahSathi.Services;

namespace RaahSathi.Controllers.Api
{
    [ApiController]
    [Route("api")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth-policy")]
    public class AuthApiController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthApiController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.AuthenticateAsync(request);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.RegisterUserAsync(request);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }

        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.SendOtpAsync(request);
            return Ok(result);
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequestDto request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _authService.VerifyOtpAsync(request);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}
