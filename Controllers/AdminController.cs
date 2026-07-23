using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;

namespace RaahSathi.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public AdminController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private bool IsAdmin()
        {
            string? adminIdStr = Request.Cookies["RaahSathiAdminUserId"];
            if (!string.IsNullOrEmpty(adminIdStr) && int.TryParse(adminIdStr, out int adminId))
            {
                var u = _dbContext.Users.Find(adminId);
                if (u != null && u.Role == "Admin") return true;
            }

            string? role = Request.Cookies["RaahSathiUserRole"];
            string? userIdStr = Request.Cookies["RaahSathiUserId"];
            if (role == "Admin" && !string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
            {
                var u = _dbContext.Users.Find(userId);
                if (u != null && u.Role == "Admin") return true;
            }

            return false; // Strict access: Deny access if not logged in as Admin
        }

        public async Task<IActionResult> Dashboard()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            // Stats
            int onlineMechanics = await _dbContext.MechanicProfiles.CountAsync(m => m.IsOnline);
            int activeRequests = await _dbContext.Jobs.CountAsync(j => j.Status != "Completed" && j.Status != "Cancelled");
            int totalJobs = await _dbContext.Jobs.CountAsync(j => j.Status == "Completed");
            double totalRevenue = await _dbContext.Payments.Where(p => p.PaymentStatus == "Released").SumAsync(p => p.Amount);
            
            // Pending KYC Mechanics
            var pendingMechanics = await _dbContext.MechanicProfiles
                .Include(m => m.User)
                .Where(m => m.KycStatus == "Pending")
                .ToListAsync();

            // Pricing rules
            var pricingRules = await _dbContext.PricingRules.ToListAsync();

            // Disputes list
            var disputes = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Include(j => j.Vehicle)
                .Where(j => j.DisputeStatus == "Active")
                .ToListAsync();

            ViewBag.OnlineMechanics = onlineMechanics;
            ViewBag.ActiveRequests = activeRequests;
            ViewBag.TotalJobs = totalJobs;
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.PendingMechanics = pendingMechanics;
            ViewBag.PricingRules = pricingRules;
            ViewBag.Disputes = disputes;

            return View();
        }


        [HttpPost("/Admin/UpdatePricingRule")]
        [HttpPost("/Admin/UpdatePricing")]
        public async Task<IActionResult> UpdatePricingRule(int ruleId, double baseFee, double perKmRate, double baseTowingFee = 0, double perKmTowingRate = 0, double baseTowing = 0, double perKmTowing = 0)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var rule = await _dbContext.PricingRules.FindAsync(ruleId);
            if (rule != null)
            {
                rule.BaseFee = baseFee;
                rule.PerKmRate = perKmRate;
                rule.BaseTowingFee = baseTowingFee > 0 ? baseTowingFee : baseTowing;
                rule.PerKmTowingRate = perKmTowingRate > 0 ? perKmTowingRate : perKmTowing;
                
                await _dbContext.SaveChangesAsync();
                TempData["Success"] = $"Pricing rates updated for: {rule.VehicleCategory}";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> ResolveDispute(int jobId, string resolutionText, string outcome)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var job = await _dbContext.Jobs.FindAsync(jobId);
            var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.JobId == jobId);

            if (job == null) return NotFound();

            job.DisputeStatus = "Resolved";
            job.DisputeResolution = resolutionText;

            if (payment != null)
            {
                if (outcome == "Refund")
                {
                    payment.PaymentStatus = "Refunded";
                    // Refund to customer wallet/UPI (mock)
                    job.Status = "Cancelled";
                }
                else
                {
                    payment.PaymentStatus = "Released";
                    // Release to mechanic wallet (mock)
                    job.Status = "Completed";
                }
            }

            await _dbContext.SaveChangesAsync();
            TempData["Success"] = $"Dispute for Job #{jobId} resolved. Action: {outcome}";
            return RedirectToAction("Dashboard");
        }

        // ======================= Manage Users CRUD ======================= //

        public async Task<IActionResult> ManageUsers()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            ViewBag.Users = await _dbContext.Users.OrderByDescending(u => u.Id).ToListAsync();
            ViewBag.Vehicles = await _dbContext.Vehicles.ToListAsync();
            ViewBag.MechanicProfiles = await _dbContext.MechanicProfiles.ToListAsync();
            ViewBag.Jobs = await _dbContext.Jobs.Include(j => j.Vehicle).OrderByDescending(j => j.Id).ToListAsync();
            ViewBag.Complaints = await _dbContext.MechanicComplaints
                .Include(c => c.Customer)
                .Include(c => c.Mechanic)
                .Include(c => c.Job)
                .OrderByDescending(c => c.Id)
                .ToListAsync();
            ViewBag.Warnings = await _dbContext.MechanicWarnings
                .Include(w => w.Mechanic)
                .OrderByDescending(w => w.Id)
                .ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendMechanicWarning(int complaintId, int mechanicId, string warningType, string message)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var mechanic = await _dbContext.Users.FindAsync(mechanicId);
            if (mechanic == null) return NotFound();

            var warning = new MechanicWarning
            {
                MechanicId = mechanicId,
                ComplaintId = complaintId > 0 ? complaintId : null,
                WarningType = string.IsNullOrEmpty(warningType) ? "Official Warning" : warningType,
                Message = message,
                IsAcknowledged = false,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.MechanicWarnings.Add(warning);

            if (complaintId > 0)
            {
                var complaint = await _dbContext.MechanicComplaints.FindAsync(complaintId);
                if (complaint != null)
                {
                    complaint.Status = "WarningSent";
                }
            }

            await _dbContext.SaveChangesAsync();
            TempData["Success"] = $"Warning successfully issued to Mechanic {mechanic.Name} (URA-{mechanic.Id}). Red Alert Popup will trigger on their dashboard.";
            return RedirectToAction("ManageUsers");
        }

        [HttpPost]
        public async Task<IActionResult> DismissComplaint(int complaintId)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var complaint = await _dbContext.MechanicComplaints.FindAsync(complaintId);
            if (complaint != null)
            {
                complaint.Status = "Dismissed";
                await _dbContext.SaveChangesAsync();
                TempData["Success"] = "Complaint marked as dismissed.";
            }

            return RedirectToAction("ManageUsers");
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(string name, string phoneNumber, string role)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var user = new User { Name = name, PhoneNumber = phoneNumber, Role = role, Password = "1234" };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            if (role == "Mechanic")
            {
                _dbContext.MechanicProfiles.Add(new MechanicProfile { UserId = user.Id, KycStatus = "Incomplete", IsOnline = false, CommissionRate = 0.20, SkillCategory = "Car", ExperienceYears = 1, Rating = 5.0 });
                await _dbContext.SaveChangesAsync();
            }

            TempData["Success"] = $"User {name} added successfully.";
            return RedirectToAction("ManageUsers");
        }

        public async Task<IActionResult> ReviewKyc(int id, bool partial = false)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var profile = await _dbContext.MechanicProfiles
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.UserId == id);
                
            if (profile == null) return RedirectToAction("ManageUsers");
            if (partial)
            {
                ViewData["IsPartial"] = true;
            }
            return View(profile);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveKyc(int userId, string status, bool? approve)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var profile = await _dbContext.MechanicProfiles.FindAsync(userId);
            if (profile != null)
            {
                // Fallback for cached forms sending 'approve' instead of 'status'
                if (string.IsNullOrEmpty(status) && approve.HasValue)
                {
                    status = approve.Value ? "Approved" : "Rejected";
                }

                if (!string.IsNullOrEmpty(status))
                {
                    profile.KycStatus = status;
                    if (status == "Suspended" || status == "Rejected")
                    {
                        profile.IsOnline = false;
                    }
                    await _dbContext.SaveChangesAsync();
                    TempData["Success"] = $"KYC status updated to {status}.";
                }
            }
            
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, status = status });
            }

            string referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var user = await _dbContext.Users.FindAsync(id);
            if (user != null)
            {
                var profile = await _dbContext.MechanicProfiles.FindAsync(id);
                if (profile != null) _dbContext.MechanicProfiles.Remove(profile);
                
                _dbContext.Users.Remove(user);
                await _dbContext.SaveChangesAsync();
                TempData["Success"] = "User deleted successfully.";
            }
            return RedirectToAction("ManageUsers");
        }

        // ======================= Manage Pricing CRUD ======================= //

        public async Task<IActionResult> ManagePricing()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            ViewBag.PricingRules = await _dbContext.PricingRules.ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddPricingRule(string vehicleCategory, double baseFee, double perKmRate, double baseTowingFee, double perKmTowingRate)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var rule = new PricingRule 
            { 
                VehicleCategory = vehicleCategory, 
                BaseFee = baseFee, 
                PerKmRate = perKmRate, 
                BaseTowingFee = baseTowingFee, 
                PerKmTowingRate = perKmTowingRate 
            };
            _dbContext.PricingRules.Add(rule);
            await _dbContext.SaveChangesAsync();

            TempData["Success"] = $"Pricing Rule for {vehicleCategory} added successfully.";
            return RedirectToAction("ManagePricing");
        }

        [HttpPost]
        public async Task<IActionResult> DeletePricingRule(int id)
        {
            var rule = await _dbContext.PricingRules.FindAsync(id);
            if (rule != null)
            {
                _dbContext.PricingRules.Remove(rule);
                await _dbContext.SaveChangesAsync();
                TempData["Success"] = "Pricing Rule deleted successfully.";
            }
            return RedirectToAction("ManagePricing");
        }

        public async Task<IActionResult> Messages(string? statusFilter)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            try
            {
                await _dbContext.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[ContactMessages]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [ContactMessages] (
                            [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [FullName] nvarchar(200) NOT NULL,
                            [Phone] nvarchar(50) NOT NULL,
                            [Email] nvarchar(200) NOT NULL DEFAULT '',
                            [Subject] nvarchar(200) NOT NULL DEFAULT 'General Inquiry',
                            [Message] nvarchar(max) NOT NULL,
                            [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                            [Status] nvarchar(50) NOT NULL DEFAULT 'Pending',
                            [AdminNotes] nvarchar(max) NULL,
                            [ContactedAt] datetime2 NULL
                        );
                    END;
                ");
            }
            catch { }

            var query = _dbContext.ContactMessages.AsQueryable();

            ViewBag.TotalCount = await _dbContext.ContactMessages.CountAsync();
            ViewBag.PendingCount = await _dbContext.ContactMessages.CountAsync(m => m.Status == "Pending");
            ViewBag.ContactedCount = await _dbContext.ContactMessages.CountAsync(m => m.Status == "Contacted");
            ViewBag.ResolvedCount = await _dbContext.ContactMessages.CountAsync(m => m.Status == "Resolved");

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                query = query.Where(m => m.Status == statusFilter);
            }

            ViewBag.StatusFilter = statusFilter ?? "All";

            var messages = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
            return View(messages);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateMessageStatus(int id, string status, string? adminNotes)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var msg = await _dbContext.ContactMessages.FindAsync(id);
            if (msg == null) return Json(new { success = false, message = "Message not found" });

            msg.Status = status;
            msg.AdminNotes = adminNotes;
            if (status == "Contacted" || status == "Resolved")
            {
                msg.ContactedAt = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return Json(new { success = true, message = "Message status updated successfully." });
        }

        [HttpPost]
        public async Task<IActionResult> SendSupportMessageToMechanic(int mechanicId, string title, string message)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, message = "Message content is required." });
            }

            var msg = new MechanicSupportMessage
            {
                MechanicId = mechanicId,
                Title = string.IsNullOrWhiteSpace(title) ? "Support & Operations Notice" : title.Trim(),
                MessageText = message.Trim(),
                SenderRole = "Admin",
                SenderName = "RaahSathi Operations Team",
                IsFromAdmin = true,
                IsRead = false,
                SentAt = DateTime.UtcNow
            };

            _dbContext.MechanicSupportMessages.Add(msg);
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Support message sent to mechanic inbox successfully!" });
        }
    }
}
