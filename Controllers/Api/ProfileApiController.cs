using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RaahSathi.DTOs;
using RaahSathi.Services;

namespace RaahSathi.Controllers.Api
{
    [ApiController]
    [Route("api")]
    public class ProfileApiController : ControllerBase
    {
        private readonly IUserService _userService;

        public ProfileApiController(IUserService userService)
        {
            _userService = userService;
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile([FromQuery] int userId)
        {
            if (userId <= 0) return BadRequest(new { success = false, message = "Valid userId is required." });
            var profile = await _userService.GetUserProfileAsync(userId);
            if (profile == null) return NotFound(new { success = false, message = "User profile not found." });
            return Ok(new { success = true, profile });
        }

        [HttpPost("profile/update")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            bool success = await _userService.UpdateUserProfileAsync(dto);
            if (!success) return BadRequest(new { success = false, message = "Failed to update profile." });
            return Ok(new { success = true, message = "Profile updated successfully." });
        }
    }
}
