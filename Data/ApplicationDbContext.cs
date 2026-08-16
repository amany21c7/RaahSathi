using Microsoft.EntityFrameworkCore;
using RaahSathi.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RaahSathi.Data
{
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<MechanicProfile> MechanicProfiles { get; set; }
        public DbSet<PricingRule> PricingRules { get; set; }
        public DbSet<Job> Jobs { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<MechanicComplaint> MechanicComplaints { get; set; }
        public DbSet<MechanicWarning> MechanicWarnings { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<JobChatMessage> JobChatMessages { get; set; }
        public DbSet<MechanicSupportMessage> MechanicSupportMessages { get; set; }
        public DbSet<ProblemTypePricing> ProblemTypePricings { get; set; }
        public DbSet<AdminWithdrawal> AdminWithdrawals { get; set; }
        public DbSet<CityServiceArea> CityServiceAreas { get; set; }
        public DbSet<CustomService> CustomServices { get; set; }
        public DbSet<CmsBanner> CmsBanners { get; set; }
        public DbSet<PushNotificationLog> PushNotificationLogs { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<AdminSystemSetting> AdminSystemSettings { get; set; }
        public DbSet<MechanicPayoutRequest> MechanicPayoutRequests { get; set; }
        public DbSet<ReferralProgramSetting> ReferralProgramSettings { get; set; }
        public DbSet<ReferralTransaction> ReferralTransactions { get; set; }
        public DbSet<ReferralWithdrawalRequest> ReferralWithdrawalRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure MechanicProfile - User one-to-one relationship
            modelBuilder.Entity<MechanicProfile>()
                .HasKey(m => m.UserId);

            modelBuilder.Entity<MechanicProfile>()
                .HasOne(m => m.User)
                .WithOne()
                .HasForeignKey<MechanicProfile>(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Job relationships
            modelBuilder.Entity<Job>()
                .HasOne(j => j.Customer)
                .WithMany()
                .HasForeignKey(j => j.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Job>()
                .HasOne(j => j.Mechanic)
                .WithMany()
                .HasForeignKey(j => j.MechanicId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Job>()
                .HasOne(j => j.Vehicle)
                .WithMany()
                .HasForeignKey(j => j.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure MechanicComplaint relationships
            modelBuilder.Entity<MechanicComplaint>()
                .HasOne(c => c.Job)
                .WithMany()
                .HasForeignKey(c => c.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MechanicComplaint>()
                .HasOne(c => c.Customer)
                .WithMany()
                .HasForeignKey(c => c.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MechanicComplaint>()
                .HasOne(c => c.Mechanic)
                .WithMany()
                .HasForeignKey(c => c.MechanicId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure MechanicWarning relationships
            modelBuilder.Entity<MechanicWarning>()
                .HasOne(w => w.Mechanic)
                .WithMany()
                .HasForeignKey(w => w.MechanicId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MechanicWarning>()
                .HasOne(w => w.Complaint)
                .WithMany()
                .HasForeignKey(w => w.ComplaintId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ReferralTransaction relationships
            modelBuilder.Entity<ReferralTransaction>()
                .HasOne(r => r.ReferrerUser)
                .WithMany()
                .HasForeignKey(r => r.ReferrerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReferralTransaction>()
                .HasOne(r => r.RefereeUser)
                .WithMany()
                .HasForeignKey(r => r.RefereeUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReferralTransaction>()
                .HasOne(r => r.TriggerJob)
                .WithMany()
                .HasForeignKey(r => r.TriggerJobId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure ReferralWithdrawalRequest relationships
            modelBuilder.Entity<ReferralWithdrawalRequest>()
                .HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            string userName = "Anonymous";
            string userRole = "Admin";
            string ipAddress = "127.0.0.1";
            string userAgent = "Unknown";

            var httpContext = _httpContextAccessor?.HttpContext;
            if (httpContext != null)
            {
                if (httpContext.User?.Identity?.IsAuthenticated == true)
                {
                    userName = httpContext.User.Identity.Name ?? "User";
                    if (httpContext.User.IsInRole("Admin")) userRole = "Admin";
                    else if (httpContext.User.IsInRole("Mechanic")) userRole = "Mechanic";
                    else if (httpContext.User.IsInRole("Customer")) userRole = "Customer";
                }
                else
                {
                    if (httpContext.Request.Cookies.TryGetValue("RaahSathiUserName", out string? cookieName))
                    {
                        userName = cookieName;
                    }
                    if (httpContext.Request.Cookies.TryGetValue("RaahSathiUserRole", out string? cookieRole))
                    {
                        userRole = cookieRole;
                    }
                }

                ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                userAgent = httpContext.Request.Headers["User-Agent"].ToString();
                if (userAgent.Length > 200) userAgent = userAgent.Substring(0, 200);
            }

            var auditEntries = new List<AuditLog>();
            var entries = ChangeTracker.Entries().ToList();

            foreach (var entry in entries)
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                string entityName = entry.Entity.GetType().Name;
                if (entityName.Contains("Proxy"))
                {
                    entityName = entry.Entity.GetType().BaseType?.Name ?? entityName;
                }

                string actionType = entry.State switch
                {
                    EntityState.Added => "INSERT",
                    EntityState.Modified => "UPDATE",
                    EntityState.Deleted => "DELETE",
                    _ => "UPDATE"
                };

                var detailsList = new List<string>();
                if (entry.State == EntityState.Added)
                {
                    foreach (var prop in entry.CurrentValues.Properties)
                    {
                        var val = entry.CurrentValues[prop];
                        if (val != null && prop.Name != "Password")
                        {
                            detailsList.Add($"{prop.Name}: '{val}'");
                        }
                    }
                }
                else if (entry.State == EntityState.Deleted)
                {
                    foreach (var prop in entry.OriginalValues.Properties)
                    {
                        var val = entry.OriginalValues[prop];
                        if (val != null && prop.Name != "Password")
                        {
                            detailsList.Add($"{prop.Name}: '{val}'");
                        }
                    }
                }
                else if (entry.State == EntityState.Modified)
                {
                    foreach (var prop in entry.OriginalValues.Properties)
                    {
                        var originalVal = entry.OriginalValues[prop];
                        var currentVal = entry.CurrentValues[prop];

                        if (prop.Name != "Password" && !Equals(originalVal, currentVal))
                        {
                            detailsList.Add($"{prop.Name}: '{originalVal}' -> '{currentVal}'");
                        }
                    }
                }

                string details = $"[Low-Level] {actionType} on {entityName}. Changes: {string.Join(", ", detailsList)}";
                if (details.Length > 2000) details = details.Substring(0, 1997) + "...";

                var audit = new AuditLog
                {
                    AdminName = userName,
                    UserRole = userRole,
                    ActionType = actionType,
                    Details = details,
                    TimeStamp = DateTime.UtcNow,
                    IpAddress = ipAddress,
                    UserAgent = userAgent
                };

                auditEntries.Add(audit);
            }

            int result = await base.SaveChangesAsync(cancellationToken);

            if (auditEntries.Count > 0)
            {
                AuditLogs.AddRange(auditEntries);
                await base.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
    }
}
