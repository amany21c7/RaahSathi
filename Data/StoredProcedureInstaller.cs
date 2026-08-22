using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;

namespace RaahSathi.Data
{
    public static class StoredProcedureInstaller
    {
        public static async Task InstallStoredProceduresAsync(ApplicationDbContext dbContext)
        {
            try
            {
                // Only run when connected to relational SQL Server
                if (dbContext.Database.IsSqlServer())
                {
                    string scriptPath = Path.Combine(AppContext.BaseDirectory, "Data", "StoredProcedures.sql");
                    if (!File.Exists(scriptPath))
                    {
                        scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "StoredProcedures.sql");
                    }

                    if (File.Exists(scriptPath))
                    {
                        string fullScript = await File.ReadAllTextAsync(scriptPath);
                        var commands = fullScript.Split(new[] { "\nGO", "\r\nGO", "\nGo", "\r\nGo" }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var cmd in commands)
                        {
                            var trimmed = cmd.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmed))
                            {
                                try
                                {
                                    await dbContext.Database.ExecuteSqlRawAsync(trimmed);
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
