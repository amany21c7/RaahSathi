using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RaahSathi.DTOs;
using RaahSathi.Services;

namespace RaahSathi.Controllers.Api
{
    [ApiController]
    [Route("api")]
    public class PaymentApiController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentApiController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("payment/process")]
        [HttpPost("payment")]
        public async Task<IActionResult> ProcessPayment([FromBody] ProcessPaymentRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            bool success = await _paymentService.ProcessEscrowPaymentForJobAsync(dto.JobId, dto.PaymentId);
            if (!success)
            {
                return BadRequest(new { success = false, message = "Payment processing failed or already completed." });
            }

            var invoice = await _paymentService.GenerateJobInvoiceBreakdownAsync(dto.JobId);

            return Ok(new PaymentResultDto
            {
                Success = true,
                Message = "Escrow payment processed successfully.",
                JobId = dto.JobId,
                PaymentId = dto.PaymentId ?? invoice.InvoiceNo,
                TotalAmount = invoice.TotalBillAmount,
                AdminCommission = invoice.AdminCommission,
                MechanicEarnings = invoice.MechanicNetEarning,
                PaymentStatus = "Escrow"
            });
        }

        [HttpGet("payment/invoice/{jobId}")]
        public async Task<IActionResult> GetInvoice(int jobId)
        {
            var invoice = await _paymentService.GenerateJobInvoiceBreakdownAsync(jobId);
            if (!invoice.Success) return NotFound(new { success = false, message = invoice.Message });
            return Ok(new { success = true, invoice });
        }
    }
}
