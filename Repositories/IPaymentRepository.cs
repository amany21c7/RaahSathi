using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.Models;

namespace RaahSathi.Repositories
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetPaymentByJobIdAsync(int jobId);
        Task<List<Payment>> GetAllEscrowTransactionsAsync();
        Task<bool> ExecuteProcessJobPaymentStoredProcedureAsync(int jobId, string paymentId, double amount, double adminCommission, double mechanicEarning, double commissionRate);
        Task SaveEscrowPaymentWithFallbackAsync(Payment payment, int jobId, int? mechanicId, double mechanicEarning, double commissionRate);
    }
}
