using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public class JobInvoiceBreakdownResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public int JobId { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string CustomerAddress { get; set; } = string.Empty;
        public string VehicleModel { get; set; } = string.Empty;
        public string VehicleType { get; set; } = string.Empty;
        public string VehicleRegNumber { get; set; } = string.Empty;
        public string FuelType { get; set; } = string.Empty;
        public string MechanicName { get; set; } = string.Empty;
        public string MechanicPhone { get; set; } = string.Empty;
        public string ShopName { get; set; } = string.Empty;
        public string ShopAddress { get; set; } = string.Empty;
        public string ProblemType { get; set; } = string.Empty;

        // Charges
        public double VisitingCharge { get; set; }
        public double ServiceChargeMin { get; set; }
        public double CustomEstimateAmount { get; set; }
        public string? CustomEstimateDetails { get; set; }
        public double PartsEstimateAmount { get; set; }
        public double PartsMrp { get; set; }
        public double ExtraLabourCharge { get; set; }
        public string? PartsDetails { get; set; }
        public double TowingCharge { get; set; }

        // Financial Totals
        public double TotalBillAmount { get; set; }
        public double AdminCommission { get; set; }
        public double MechanicNetEarning { get; set; }
        public double CommissionPercent { get; set; }
    }

    public class PaymentCommissionCalculationResult
    {
        public double TotalBillAmount { get; set; }
        public double CommissionRate { get; set; }
        public double AdminCommissionAmount { get; set; }
        public double MechanicNetEarningAmount { get; set; }
    }

    public interface IPaymentService
    {
        Task<bool> ProcessEscrowPaymentForJobAsync(int jobId, string? paymentId);
        Task<JobInvoiceBreakdownResult> GenerateJobInvoiceBreakdownAsync(int jobId);
        PaymentCommissionCalculationResult CalculateTieredCommissionAndNetEarnings(double totalBillAmount, double partsAmount = 0);
        Task<List<Payment>> GetAdminEscrowTransactionsLedgerAsync();
    }
}
