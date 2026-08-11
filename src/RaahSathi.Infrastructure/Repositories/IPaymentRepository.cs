using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.Models;

namespace RaahSathi.Repositories
{
    public interface IPaymentRepository
    {
        Task<Payment?> GetPaymentByJobIdAsync(int jobId);
        Task<List<Payment>> GetAllEscrowTransactionsAsync();
        Task<bool> ExecuteProcessEscrowStoredProcedureAsync(int jobId, string paymentId);
        Task SaveEscrowPaymentWithFallbackAsync(Payment payment, int jobId, int? mechanicId, double mechanicEarning, double commissionRate);
    }
}
