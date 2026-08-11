using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RaahSathi.DTOs;
using RaahSathi.Services;

namespace RaahSathi.Controllers.Api
{
    [ApiController]
    [Route("api")]
    public class WalletApiController : ControllerBase
    {
        private readonly IWalletService _walletService;

        public WalletApiController(IWalletService walletService)
        {
            _walletService = walletService;
        }

        [HttpGet("wallet")]
        public async Task<IActionResult> GetWallet([FromQuery] int mechanicId)
        {
            if (mechanicId <= 0) return BadRequest(new { success = false, message = "Valid mechanicId is required." });
            var balance = await _walletService.GetWalletBalanceAsync(mechanicId);
            return Ok(new { success = true, wallet = balance });
        }

        [HttpPost("wallet/payout-request")]
        public async Task<IActionResult> RequestPayout([FromBody] CreatePayoutRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _walletService.RequestPayoutAsync(dto);
            if (!result.Success) return BadRequest(result);
            return Ok(result);
        }
    }
}
