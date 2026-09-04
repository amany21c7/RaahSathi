using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace RaahSathi.Services
{
    /// <summary>
    /// Background service that ensures the Node.js WhatsApp Gateway microservice 
    /// is automatically running on port 5005 whenever RaahSathi starts.
    /// </summary>
    public class WhatsAppGatewayHostedService : IHostedService
    {
        private readonly ILogger<WhatsAppGatewayHostedService> _logger;
        private Process? _gatewayProcess;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        public WhatsAppGatewayHostedService(ILogger<WhatsAppGatewayHostedService> logger)
        {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Run startup check in background so Web application starts immediately without delay
            _ = Task.Run(async () => await EnsureGatewayRunningAsync(cancellationToken));
            return Task.CompletedTask;
        }

        private async Task EnsureGatewayRunningAsync(CancellationToken ct)
        {
            try
            {
                // 1. Check if gateway is already active on port 5005
                bool isAlreadyRunning = false;
                try
                {
                    var res = await _httpClient.GetAsync("http://127.0.0.1:5005/status", ct);
                    if (res.IsSuccessStatusCode || (int)res.StatusCode == 503)
                    {
                        isAlreadyRunning = true;
                    }
                }
                catch
                {
                    isAlreadyRunning = false;
                }

                if (isAlreadyRunning)
                {
                    _logger.LogInformation("WhatsApp Gateway is already running and active on http://127.0.0.1:5005.");
                    return;
                }

                // 2. Locate the whatsapp-gateway folder
                string baseDir = Directory.GetCurrentDirectory();
                string gatewayDir = Path.Combine(baseDir, "whatsapp-gateway");
                if (!Directory.Exists(gatewayDir))
                {
                    string fallbackDir = Path.Combine(AppContext.BaseDirectory, "whatsapp-gateway");
                    if (Directory.Exists(fallbackDir))
                    {
                        gatewayDir = fallbackDir;
                    }
                    else
                    {
                        _logger.LogWarning("whatsapp-gateway directory not found at {Path}. Manual launch required.", gatewayDir);
                        return;
                    }
                }

                string serverScript = Path.Combine(gatewayDir, "server.js");
                if (!File.Exists(serverScript))
                {
                    _logger.LogWarning("server.js not found in {Dir}. Cannot auto-start WhatsApp Gateway.", gatewayDir);
                    return;
                }

                _logger.LogInformation("Auto-starting WhatsApp Gateway microservice from {Dir}...", gatewayDir);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = "server.js",
                    WorkingDirectory = gatewayDir,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _gatewayProcess = Process.Start(startInfo);

                if (_gatewayProcess != null && !_gatewayProcess.HasExited)
                {
                    _logger.LogInformation("WhatsApp Gateway successfully auto-started (PID: {Pid}) on port 5005.", _gatewayProcess.Id);

                    // Wait 3 seconds and verify connection
                    await Task.Delay(3000, ct);
                    try
                    {
                        var verifyRes = await _httpClient.GetAsync("http://127.0.0.1:5005/status", ct);
                        _logger.LogInformation("WhatsApp Gateway healthcheck status: {Status}", verifyRes.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Initial WhatsApp Gateway healthcheck note: {Message}", ex.Message);
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to launch WhatsApp Gateway process. Please ensure Node.js is installed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while auto-starting WhatsApp Gateway microservice.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_gatewayProcess != null && !_gatewayProcess.HasExited)
                {
                    _logger.LogInformation("Stopping WhatsApp Gateway microservice (PID: {Pid})...", _gatewayProcess.Id);
                    _gatewayProcess.Kill(entireProcessTree: true);
                    _gatewayProcess.Dispose();
                    _gatewayProcess = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Error while terminating WhatsApp Gateway process: {Msg}", ex.Message);
            }

            return Task.CompletedTask;
        }
    }
}
