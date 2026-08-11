using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RaahSathi.Services;

namespace RaahSathi.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IPaymentService _paymentService;

        public PaymentController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost]
        public async Task<IActionResult> ProcessEscrowPayment(int jobId, string? paymentId)
        {
            if (jobId <= 0)
            {
                return Json(new { success = false, message = "Invalid Job ID provided." });
            }

            bool success = await _paymentService.ProcessEscrowPaymentForJobAsync(jobId, paymentId);
            if (!success)
            {
                return Json(new { success = false, message = "Failed to process escrow payment or job not found." });
            }

            return Json(new { success = true, message = "Payment successfully escrowed and released." });
        }

        [HttpGet]
        public async Task<IActionResult> GetJobInvoiceBreakdownDetails(int jobId)
        {
            var invoiceBreakdown = await _paymentService.GenerateJobInvoiceBreakdownAsync(jobId);
            return Json(invoiceBreakdown);
        }

        [HttpGet]
        public async Task<IActionResult> GetEscrowTransactionsLedger()
        {
            var transactions = await _paymentService.GetAdminEscrowTransactionsLedgerAsync();
            return Json(new { success = true, transactions });
        }
    }
}
