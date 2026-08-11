using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RaahSathi.DTOs;
using RaahSathi.Services;

namespace RaahSathi.Controllers.Api
{
    [ApiController]
    [Route("api")]
    public class JobApiController : ControllerBase
    {
        private readonly IJobService _jobService;

        public JobApiController(IJobService jobService)
        {
            _jobService = jobService;
        }

        [HttpPost("createjob")]
        public async Task<IActionResult> CreateJob([FromBody] CreateJobRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var result = await _jobService.CreateJobAsync(dto);
            if (result == null) return BadRequest(new { success = false, message = "Failed to create job. Check vehicle details." });
            return Ok(new { success = true, job = result });
        }

        [HttpGet("getjobs")]
        public async Task<IActionResult> GetJobs([FromQuery] int? customerId, [FromQuery] int? mechanicId)
        {
            if (customerId.HasValue)
            {
                var jobs = await _jobService.GetCustomerJobsAsync(customerId.Value);
                return Ok(new { success = true, count = jobs.Count, jobs });
            }

            if (mechanicId.HasValue)
            {
                var jobs = await _jobService.GetMechanicJobsAsync(mechanicId.Value);
                return Ok(new { success = true, count = jobs.Count, jobs });
            }

            return BadRequest(new { success = false, message = "Either customerId or mechanicId must be provided." });
        }

        [HttpGet("job/{id}")]
        public async Task<IActionResult> GetJobById(int id)
        {
            var job = await _jobService.GetJobDetailsAsync(id);
            if (job == null) return NotFound(new { success = false, message = "Job not found." });
            return Ok(new { success = true, job });
        }

        [HttpPost("job/accept")]
        public async Task<IActionResult> AcceptJob([FromBody] AcceptJobRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var success = await _jobService.AcceptJobAsync(dto.JobId, dto.MechanicId);
            if (!success) return BadRequest(new { success = false, message = "Could not accept job." });
            return Ok(new { success = true, message = "Job accepted successfully." });
        }

        [HttpPost("job/update-status")]
        public async Task<IActionResult> UpdateStatus([FromBody] JobStatusUpdateDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var success = await _jobService.UpdateJobStatusAsync(dto.JobId, dto.Status);
            if (!success) return BadRequest(new { success = false, message = "Could not update status." });
            return Ok(new { success = true, message = $"Job status updated to {dto.Status}." });
        }
    }
}
