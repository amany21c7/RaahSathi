using System.Collections.Generic;
using System.Threading.Tasks;
using RaahSathi.Models;

namespace RaahSathi.Services
{
    public interface INotificationService
    {
        Task<PushNotificationLog> SendNotificationAsync(string audience, string city, string title, string message);
        Task<List<PushNotificationLog>> GetNotificationLogsAsync();
        Task<List<JobChatMessage>> GetJobChatMessagesAsync(int jobId);
        Task<JobChatMessage> AddChatMessageAsync(int jobId, int senderId, string message);
    }
}
