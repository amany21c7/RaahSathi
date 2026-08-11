using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _dbContext;

        public NotificationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PushNotificationLog> SendNotificationAsync(string audience, string city, string title, string message)
        {
            var log = new PushNotificationLog
            {
                TargetAudience = string.IsNullOrWhiteSpace(audience) ? "All Users" : audience,
                SelectedCity = string.IsNullOrWhiteSpace(city) ? "All" : city,
                Title = title,
                Message = message,
                SentCount = 100, // Simulated broadcast count
                SentAt = DateTime.UtcNow
            };

            _dbContext.PushNotificationLogs.Add(log);
            await _dbContext.SaveChangesAsync();
            return log;
        }

        public async Task<List<PushNotificationLog>> GetNotificationLogsAsync()
        {
            return await _dbContext.PushNotificationLogs
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();
        }

        public async Task<List<JobChatMessage>> GetJobChatMessagesAsync(int jobId)
        {
            return await _dbContext.JobChatMessages
                .Where(c => c.JobId == jobId)
                .OrderBy(c => c.SentAt)
                .ToListAsync();
        }

        public async Task<JobChatMessage> AddChatMessageAsync(int jobId, int senderId, string message)
        {
            var chat = new JobChatMessage
            {
                JobId = jobId,
                SenderId = senderId,
                MessageText = message,
                SentAt = DateTime.UtcNow
            };

            _dbContext.JobChatMessages.Add(chat);
            await _dbContext.SaveChangesAsync();
            return chat;
        }
    }
}
