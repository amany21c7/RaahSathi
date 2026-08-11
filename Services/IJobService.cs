using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.DTOs;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public interface IJobService
    {
        Task<JobDetailDto?> CreateJobAsync(CreateJobRequestDto dto);
        Task<Job?> GetJobByIdAsync(int jobId);
        Task<JobDetailDto?> GetJobDetailsAsync(int jobId);
        Task<List<JobDetailDto>> GetCustomerJobsAsync(int customerId);
        Task<List<JobDetailDto>> GetMechanicJobsAsync(int mechanicId);
        Task<bool> AcceptJobAsync(int jobId, int mechanicId);
        Task<bool> DeclineJobAsync(int jobId, int mechanicId);
        Task<bool> UpdateJobStatusAsync(int jobId, string status);
        Task<bool> CancelJobAsync(int jobId, string reason);
    }
}
