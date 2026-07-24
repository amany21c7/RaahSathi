using Microsoft.EntityFrameworkCore;
using RaahSathi.Models;

namespace RaahSathi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
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
        }
    }
}
