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
        private readonly Services.IPricingService _pricingService;

        public AdminController(ApplicationDbContext dbContext, Services.IPricingService pricingService)
        {
            _dbContext = dbContext;
            _pricingService = pricingService;
        }

        private bool IsAdmin()
        {
            return User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
        }

        public async Task<IActionResult> Dashboard()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            // Stats
            int onlineMechanics = await _dbContext.MechanicProfiles.CountAsync(m => m.IsOnline);
            int activeRequests = await _dbContext.Jobs.CountAsync(j => j.Status != "Completed" && j.Status != "Cancelled");
            int totalJobs = await _dbContext.Jobs.CountAsync(j => j.Status == "Completed");
            double totalRevenue = await _dbContext.Payments.Where(p => p.PaymentStatus == "Released").SumAsync(p => (double?)p.Amount) ?? 0.0;
            
            // Tiered Admin Commission Vault Calculation (using dynamic phase/parts rates)
            var releasedPayments = await _dbContext.Payments.Where(p => p.PaymentStatus == "Released").ToListAsync();
            double rate1 = (await GetSettingDoubleAsync("CommissionPhase1", 8)) / 100.0;
            double rate2 = (await GetSettingDoubleAsync("CommissionPhase2", 10)) / 100.0;
            double rate3 = (await GetSettingDoubleAsync("CommissionPhase3", 12)) / 100.0;
            double totalCommissionEarned = releasedPayments.Sum(p => p.AdminCommissionAmount > 0 
                ? p.AdminCommissionAmount 
                : (p.Amount < 1000 ? p.Amount * rate1 : (p.Amount <= 3000 ? p.Amount * rate2 : p.Amount * rate3)));
            double totalWithdrawn = await _dbContext.AdminWithdrawals.SumAsync(w => (double?)w.Amount) ?? 0.0;
            double adminVaultBalance = Math.Max(0.0, Math.Round(totalCommissionEarned - totalWithdrawn, 2));
            var withdrawalHistory = await _dbContext.AdminWithdrawals.OrderByDescending(w => w.WithdrawnAt).Take(10).ToListAsync();

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
            ViewBag.TotalCommissionEarned = totalCommissionEarned;
            ViewBag.TotalWithdrawn = totalWithdrawn;
            ViewBag.AdminVaultBalance = adminVaultBalance;
            ViewBag.WithdrawalHistory = withdrawalHistory;
            ViewBag.PendingMechanics = pendingMechanics;
            ViewBag.PricingRules = pricingRules;
            ViewBag.Disputes = disputes;
            ViewBag.CityAreas = await _dbContext.CityServiceAreas.ToListAsync();
            var emergencySetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "EmergencyMode");
            ViewBag.IsGlobalEmergency = emergencySetting?.SettingValue?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true || emergencySetting?.SettingValue?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

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
            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
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
                var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == id);
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
            ViewBag.ProblemTypes = await _dbContext.ProblemTypePricings.OrderBy(p => p.VehicleCategory).ThenBy(p => p.ProblemName).ToListAsync();
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
            return RedirectToAction("Pricing");
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

        [HttpPost]
        public async Task<IActionResult> AddProblemType(string problemName, string vehicleCategory, string cityName, double minServiceCharge, double maxServiceCharge)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            bool success = await _pricingService.AddNewProblemPriceRateAsync(problemName, vehicleCategory, cityName, minServiceCharge, maxServiceCharge);
            if (!success)
            {
                TempData["Error"] = "Please enter valid problem pricing details.";
                return RedirectToAction("Pricing");
            }

            await LogAdminActionAsync("ADD_PROBLEM_PRICE", $"Added Problem '{problemName}' ({vehicleCategory}, City: {cityName}) -> Min: ₹{minServiceCharge}, Max: ₹{maxServiceCharge}");
            TempData["Success"] = $"Vehicle Problem Type '{problemName}' ({cityName}) added successfully.";
            return RedirectToAction("Pricing");
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProblemTypePrice(int id, string problemName, string vehicleCategory, string cityName, double minServiceCharge, double maxServiceCharge)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            bool success = await _pricingService.UpdateProblemPriceRateAsync(id, problemName, vehicleCategory, cityName, minServiceCharge, maxServiceCharge);
            if (!success)
            {
                TempData["Error"] = "Invalid input values or problem rate not found.";
                return RedirectToAction("Pricing");
            }

            await LogAdminActionAsync("UPDATE_PROBLEM_PRICE", $"Updated Problem '{problemName}' ({vehicleCategory}, City: {cityName}) -> Min: ₹{minServiceCharge}, Max: ₹{maxServiceCharge}");
            TempData["Success"] = $"Price rate for '{problemName}' ({cityName}) updated successfully!";
            return RedirectToAction("Pricing");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProblemType(int id)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            bool success = await _pricingService.DeleteProblemPriceRateAsync(id);
            if (success)
            {
                await LogAdminActionAsync("DELETE_PROBLEM_PRICE", $"Deleted Problem Rate ID {id}");
                TempData["Success"] = "Problem Type deleted successfully.";
            }

            return RedirectToAction("Pricing");
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

        [HttpPost("/Admin/WithdrawAdminCommission")]
        public async Task<IActionResult> WithdrawAdminCommission(double amount, string payoutMethod = "Bank Transfer", string referenceNumber = "")
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Not authenticated" });

            if (amount <= 0) return Json(new { success = false, message = "Please enter a valid withdrawal amount." });

            var releasedPayments = await _dbContext.Payments.Where(p => p.PaymentStatus == "Released").ToListAsync();
            double rate1 = (await GetSettingDoubleAsync("CommissionPhase1", 8)) / 100.0;
            double rate2 = (await GetSettingDoubleAsync("CommissionPhase2", 10)) / 100.0;
            double rate3 = (await GetSettingDoubleAsync("CommissionPhase3", 12)) / 100.0;
            double totalCommissionEarned = releasedPayments.Sum(p => p.AdminCommissionAmount > 0 
                ? p.AdminCommissionAmount 
                : (p.Amount < 1000 ? p.Amount * rate1 : (p.Amount <= 3000 ? p.Amount * rate2 : p.Amount * rate3)));
            double totalWithdrawn = await _dbContext.AdminWithdrawals.SumAsync(w => (double?)w.Amount) ?? 0.0;
            double currentVaultBalance = Math.Max(0.0, Math.Round(totalCommissionEarned - totalWithdrawn, 2));

            if (amount > currentVaultBalance)
            {
                return Json(new { success = false, message = $"Requested amount (₹{amount}) exceeds current available commission vault balance (₹{currentVaultBalance:N2})." });
            }

            string method = string.IsNullOrWhiteSpace(payoutMethod) ? "Bank Transfer" : payoutMethod;
            string refNo = string.IsNullOrWhiteSpace(referenceNumber) ? "ADM_" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper() : referenceNumber;

            // Execute Stored Procedure: rs_adminwithdrawals_insert
            await _dbContext.Database.ExecuteSqlRawAsync(
                "EXEC dbo.rs_adminwithdrawals_insert @Amount = {0}, @PayoutMethod = {1}, @ReferenceNumber = {2}",
                amount, method, refNo
            );

            return Json(new { success = true, newVaultBalance = Math.Round(currentVaultBalance - amount, 2), message = $"₹{amount:N2} successfully withdrawn from Admin Commission Vault!" });
        }

        // ======================= COMMAND CENTER MODULES ======================= //

        private async Task LogAdminActionAsync(string actionType, string details)
        {
            try
            {
                string ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                string agent = Request.Headers["User-Agent"].ToString();
                if (agent.Length > 200) agent = agent.Substring(0, 200);

                var log = new AuditLog
                {
                    AdminName = "Super Admin",
                    ActionType = actionType,
                    Details = details,
                    TimeStamp = DateTime.UtcNow,
                    IpAddress = ip,
                    UserAgent = agent
                };
                _dbContext.AuditLogs.Add(log);
                await _dbContext.SaveChangesAsync();
            }
            catch { }
        }

        [HttpGet]
        public async Task<IActionResult> GetCityEmergencyStatuses()
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var globalSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "EmergencyMode");
            bool isGlobalEmergency = globalSetting?.SettingValue?.Equals("ON", StringComparison.OrdinalIgnoreCase) == true || globalSetting?.SettingValue?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

            var cities = await _dbContext.CityServiceAreas
                .Select(c => new { 
                    c.Id, 
                    c.CityName, 
                    c.AreaName, 
                    c.State, 
                    c.ServiceRadiusKm, 
                    c.IsEmergencyMode, 
                    c.EmergencyReason 
                })
                .ToListAsync();

            return Json(new { success = true, isGlobalEmergency, cities });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleCityEmergencyMode(string cityName, bool enable, string? weatherReason)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            string reasonText = string.IsNullOrWhiteSpace(weatherReason) ? "Heavy Rain & Storm 🌧️" : weatherReason.Trim();

            if (string.IsNullOrWhiteSpace(cityName) || cityName.Equals("All Cities", StringComparison.OrdinalIgnoreCase))
            {
                var globalSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "EmergencyMode");
                if (globalSetting == null)
                {
                    globalSetting = new AdminSystemSetting { SettingKey = "EmergencyMode", SettingValue = enable ? "ON" : "OFF", Category = "Emergency", Description = reasonText };
                    _dbContext.AdminSystemSettings.Add(globalSetting);
                }
                else
                {
                    globalSetting.SettingValue = enable ? "ON" : "OFF";
                    globalSetting.Description = reasonText;
                }

                var allCities = await _dbContext.CityServiceAreas.ToListAsync();
                foreach (var c in allCities)
                {
                    c.IsEmergencyMode = enable;
                    c.EmergencyReason = reasonText;
                }

                await _dbContext.SaveChangesAsync();
                await LogAdminActionAsync("EMERGENCY_MODE_ALL", $"All Cities Emergency Mode set to {(enable ? "ON (+12%)" : "OFF")}. Reason: {reasonText}");

                return Json(new { success = true, isEmergency = enable, cityName = "All Cities", message = $"Emergency Surge Mode is now {(enable ? "ACTIVE (+12%) 🚨" : "OFF 🟢")} for ALL Cities!" });
            }
            else
            {
                // When toggling a specific city, reset global emergency setting if it was ON
                var globalSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "EmergencyMode");
                if (globalSetting != null && globalSetting.SettingValue == "ON")
                {
                    globalSetting.SettingValue = "OFF";
                }

                string cleanTarget = cityName.Trim().ToLower();
                var cityAreas = await _dbContext.CityServiceAreas
                    .Where(c => c.CityName.ToLower() == cleanTarget || c.CityName.ToLower().Contains(cleanTarget) || cleanTarget.Contains(c.CityName.ToLower()))
                    .ToListAsync();

                if (cityAreas.Count == 0)
                {
                    var allCities = await _dbContext.CityServiceAreas.ToListAsync();
                    cityAreas = allCities.Where(c => c.CityName.Trim().Equals(cleanTarget, StringComparison.OrdinalIgnoreCase) || c.CityName.ToLower().Contains(cleanTarget) || cleanTarget.Contains(c.CityName.ToLower())).ToList();
                }

                if (cityAreas.Count == 0)
                {
                    return Json(new { success = false, message = $"City '{cityName}' not found in database." });
                }

                foreach (var c in cityAreas)
                {
                    c.IsEmergencyMode = enable;
                    c.EmergencyReason = reasonText;
                }

                await _dbContext.SaveChangesAsync();
                await LogAdminActionAsync("EMERGENCY_MODE_CITY", $"Emergency Mode for {cityName} set to {(enable ? "ON (+12%)" : "OFF")}. Reason: {reasonText}");

                return Json(new { success = true, isEmergency = enable, cityName = cityName, message = $"Emergency Surge (+12%) for {cityName} is now {(enable ? "ACTIVE 🚨" : "OFF 🟢")}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleEmergencyMode(bool enable, string? reason)
        {
            return await ToggleCityEmergencyMode("All Cities", enable, reason);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUserBlock(int userId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized" });

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return Json(new { success = false, message = "User not found" });

            user.IsBlocked = !user.IsBlocked;
            await _dbContext.SaveChangesAsync();

            await LogAdminActionAsync(user.IsBlocked ? "BLOCK_USER" : "UNBLOCK_USER", $"User #{user.Id} ({user.Name} - {user.Role}) set to {(user.IsBlocked ? "BLOCKED" : "ACTIVE")}");

            return Json(new { success = true, isBlocked = user.IsBlocked, message = $"User {user.Name} status updated to {(user.IsBlocked ? "BLOCKED" : "ACTIVE")}." });
        }

        public async Task<IActionResult> Customers()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var customers = await _dbContext.Users
                .Where(u => u.Role == "Customer")
                .OrderByDescending(u => u.Id)
                .ToListAsync();

            ViewBag.Vehicles = await _dbContext.Vehicles.ToListAsync();
            ViewBag.Jobs = await _dbContext.Jobs.Where(j => j.Customer != null).ToListAsync();
            ViewBag.Complaints = await _dbContext.MechanicComplaints.ToListAsync();

            return View(customers);
        }

        public async Task<IActionResult> Mechanics()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var mechanics = await _dbContext.MechanicProfiles
                .Include(m => m.User)
                .OrderByDescending(m => m.UserId)
                .ToListAsync();

            ViewBag.Jobs = await _dbContext.Jobs.ToListAsync();
            ViewBag.Complaints = await _dbContext.MechanicComplaints.ToListAsync();

            return View(mechanics);
        }

        public async Task<IActionResult> Workshops()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var workshops = await _dbContext.MechanicProfiles
                .Include(m => m.User)
                .Where(m => !string.IsNullOrEmpty(m.ShopName))
                .ToListAsync();

            return View(workshops);
        }

        public async Task<IActionResult> Requests()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var requests = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Include(j => j.Vehicle)
                .Where(j => j.Status == "Requested" || j.Status == "EstimatePending")
                .OrderByDescending(j => j.Id)
                .ToListAsync();

            return View(requests);
        }

        public async Task<IActionResult> Jobs()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var jobs = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Include(j => j.Vehicle)
                .OrderByDescending(j => j.Id)
                .ToListAsync();

            return View(jobs);
        }

        public async Task<IActionResult> LiveMap()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            ViewBag.Mechanics = await _dbContext.MechanicProfiles.Include(m => m.User).Where(m => m.IsOnline).ToListAsync();
            ViewBag.ActiveJobs = await _dbContext.Jobs.Include(j => j.Customer).Include(j => j.Mechanic).Where(j => j.Status != "Completed" && j.Status != "Cancelled").ToListAsync();

            return View();
        }

        public async Task<IActionResult> Pricing()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            ViewBag.PricingRules = await _pricingService.GetAllBaseCategoryPricingRulesAsync();
            ViewBag.ProblemTypes = await _pricingService.GetAllActiveProblemPricesAsync();
            ViewBag.Cities = await _dbContext.CityServiceAreas.ToListAsync();

            ViewBag.CommPhase1 = await GetSettingIntAsync("CommissionPhase1", 8);
            ViewBag.CommPhase2 = await GetSettingIntAsync("CommissionPhase2", 10);
            ViewBag.CommPhase3 = await GetSettingIntAsync("CommissionPhase3", 12);
            ViewBag.CommParts = await GetSettingIntAsync("CommissionParts", 5);

            return View();
        }

        public async Task<IActionResult> Services()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var services = await _dbContext.CustomServices.OrderByDescending(s => s.Id).ToListAsync();
            return View(services);
        }

        [HttpPost]
        public async Task<IActionResult> AddCustomService(string serviceName, string category, double basePrice, double maxPrice, string description, string iconClass)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var svc = new CustomService
            {
                ServiceName = serviceName.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "Breakdown" : category,
                BasePrice = basePrice,
                MaxPrice = maxPrice,
                Description = description ?? "",
                IconClass = string.IsNullOrWhiteSpace(iconClass) ? "fa-screwdriver-wrench" : iconClass,
                IsActive = true
            };
            _dbContext.CustomServices.Add(svc);
            await _dbContext.SaveChangesAsync();
            await LogAdminActionAsync("ADD_SERVICE", $"Added custom service: {serviceName} (₹{basePrice}-{maxPrice})");

            TempData["Success"] = $"Service '{serviceName}' added successfully.";
            return RedirectToAction("Services");
        }

        public async Task<IActionResult> Cities()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var cities = await _dbContext.CityServiceAreas.OrderBy(c => c.State).ThenBy(c => c.CityName).ToListAsync();
            return View(cities);
        }

        [HttpPost]
        public async Task<IActionResult> AddCityArea(string state, string cityName, string areaName, double serviceRadiusKm)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var city = new CityServiceArea
            {
                State = string.IsNullOrWhiteSpace(state) ? "Uttar Pradesh" : state.Trim(),
                CityName = string.IsNullOrWhiteSpace(cityName) ? "Noida" : cityName.Trim(),
                AreaName = string.IsNullOrWhiteSpace(areaName) ? "Sector 62" : areaName.Trim(),
                ServiceRadiusKm = serviceRadiusKm > 0 ? serviceRadiusKm : 15.0,
                IsActive = true
            };
            _dbContext.CityServiceAreas.Add(city);
            await _dbContext.SaveChangesAsync();
            await LogAdminActionAsync("ADD_CITY", $"Added City Service Area: {cityName} - {areaName} ({serviceRadiusKm} KM)");

            TempData["Success"] = $"City area '{cityName} ({areaName})' added.";
            return RedirectToAction("Cities");
        }

        public async Task<IActionResult> Vehicles()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var vehicles = await _dbContext.Vehicles.Include(v => v.User).OrderByDescending(v => v.Id).ToListAsync();
            return View(vehicles);
        }

        public async Task<IActionResult> Payments()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var payments = await _dbContext.Payments.OrderByDescending(p => p.Id).ToListAsync();
            ViewBag.Withdrawals = await _dbContext.AdminWithdrawals.OrderByDescending(w => w.WithdrawnAt).ToListAsync();

            // Load pending and processed payout requests
            var requests = await _dbContext.MechanicPayoutRequests
                .Join(_dbContext.Users,
                      r => r.MechanicId,
                      u => u.Id,
                      (r, u) => new PayoutRequestViewModel 
                      { 
                          Request = r, 
                          MechanicName = u.Name, 
                          PhoneNumber = u.PhoneNumber,
                          DisplayId = u.Role == "Mechanic" ? $"RS{u.Id:D2}M" : u.Id.ToString()
                      })
                .OrderByDescending(x => x.Request.CreatedAt)
                .ToListAsync();

            // Populate cities for each view model from profile
            foreach (var r in requests)
            {
                var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == r.Request.MechanicId);
                r.City = profile?.City ?? "Noida";
            }

            ViewBag.PayoutRequests = requests;

            // Load mechanics ledger (city-wise)
            var mechanics = await _dbContext.MechanicProfiles
                .Join(_dbContext.Users,
                      p => p.UserId,
                      u => u.Id,
                      (p, u) => new MechanicLedgerViewModel
                      {
                          UserId = p.UserId,
                          Name = u.Name,
                          PhoneNumber = u.PhoneNumber,
                          DisplayId = u.Role == "Mechanic" ? $"RS{u.Id:D2}M" : u.Id.ToString(),
                          City = p.City ?? "Noida",
                          CurrentEarnings = p.CurrentEarnings,
                          TotalJobs = p.TotalJobs,
                          Rating = p.Rating,
                          PreferredPayoutMethod = p.PreferredPayoutMethod ?? "UPI",
                          BankName = p.BankName ?? string.Empty,
                          BankAccountNumber = p.BankAccountNumber ?? string.Empty,
                          IfscCode = p.IfscCode ?? string.Empty,
                          UpiId = p.UpiId ?? string.Empty,
                          AccountHolderName = p.AccountHolderName ?? string.Empty
                      })
                .OrderBy(m => m.City)
                .ThenBy(m => m.Name)
                .ToListAsync();

            // Populate pending payout amount for each mechanic
            foreach (var m in mechanics)
            {
                m.PendingPayoutAmount = await _dbContext.MechanicPayoutRequests
                    .Where(r => r.MechanicId == m.UserId && r.Status == "Pending")
                    .SumAsync(r => r.Amount);
            }

            ViewBag.Mechanics = mechanics;

            return View(payments);
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePayoutRequest(int requestId, string referenceNumber, string remarks)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var req = await _dbContext.MechanicPayoutRequests.FindAsync(requestId);
            if (req == null) return NotFound();

            if (req.Status != "Pending")
            {
                TempData["Error"] = "This request has already been processed.";
                return RedirectToAction("Payments");
            }

            req.Status = "Approved";
            req.ProcessedAt = DateTime.UtcNow;
            req.TransactionReference = string.IsNullOrEmpty(referenceNumber) ? Guid.NewGuid().ToString().Substring(0, 12).ToUpper() : referenceNumber;
            req.AdminRemarks = string.IsNullOrEmpty(remarks) ? "Payout released by Admin" : remarks;

            var adminWithdrawal = new AdminWithdrawal
            {
                Amount = req.Amount,
                PayoutMethod = req.PayoutMethod == "UPI" ? "UPI Direct" : "Bank Transfer",
                ReferenceNumber = req.TransactionReference,
                WithdrawnAt = DateTime.UtcNow
            };
            _dbContext.AdminWithdrawals.Add(adminWithdrawal);

            var supportMsg = new MechanicSupportMessage
            {
                MechanicId = req.MechanicId,
                Title = "💰 Payout Released",
                MessageText = $"Your payout request for ₹{req.Amount:N2} has been approved and released.\nMethod: {req.PayoutMethod}\nReference Number: {req.TransactionReference}\nRemarks: {req.AdminRemarks}",
                SenderRole = "Admin",
                SenderName = "RaahSathi Finance Desk",
                IsFromAdmin = true,
                IsRead = false,
                SentAt = DateTime.UtcNow
            };
            _dbContext.MechanicSupportMessages.Add(supportMsg);

            await _dbContext.SaveChangesAsync();

            TempData["Success"] = $"Payout of ₹{req.Amount:N2} approved and released successfully!";
            return RedirectToAction("Payments");
        }

        [HttpPost]
        public async Task<IActionResult> RejectPayoutRequest(int requestId, string remarks)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var req = await _dbContext.MechanicPayoutRequests.FindAsync(requestId);
            if (req == null) return NotFound();

            if (req.Status != "Pending")
            {
                TempData["Error"] = "This request has already been processed.";
                return RedirectToAction("Payments");
            }

            req.Status = "Rejected";
            req.ProcessedAt = DateTime.UtcNow;
            req.AdminRemarks = string.IsNullOrEmpty(remarks) ? "Rejected by Admin" : remarks;

            // Refund held funds
            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == req.MechanicId);
            if (profile != null)
            {
                profile.CurrentEarnings += req.Amount;
            }

            var supportMsg = new MechanicSupportMessage
            {
                MechanicId = req.MechanicId,
                Title = "❌ Payout Request Rejected",
                MessageText = $"Your payout request for ₹{req.Amount:N2} was rejected by Admin.\nReason: {req.AdminRemarks}\nAmount has been refunded to your wallet balance.",
                SenderRole = "Admin",
                SenderName = "RaahSathi Finance Desk",
                IsFromAdmin = true,
                IsRead = false,
                SentAt = DateTime.UtcNow
            };
            _dbContext.MechanicSupportMessages.Add(supportMsg);

            await _dbContext.SaveChangesAsync();

            TempData["Success"] = $"Payout request of ₹{req.Amount:N2} rejected. Funds returned to mechanic wallet.";
            return RedirectToAction("Payments");
        }

        [HttpGet]
        public async Task<IActionResult> GetMechanicJobs(int mechanicId)
        {
            if (!IsAdmin()) return Unauthorized();

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == mechanicId);
            double walletBalance = profile?.CurrentEarnings ?? 0.0;

            var pendingPayouts = await _dbContext.MechanicPayoutRequests
                .Where(r => r.MechanicId == mechanicId && r.Status == "Pending")
                .SumAsync(r => r.Amount);

            var settledPayouts = await _dbContext.MechanicPayoutRequests
                .Where(r => r.MechanicId == mechanicId && r.Status == "Approved")
                .SumAsync(r => r.Amount);

            var jobs = await _dbContext.Jobs
                .GroupJoin(_dbContext.Payments,
                           j => j.Id,
                           p => p.JobId,
                           (j, payments) => new { j, payment = payments.FirstOrDefault() })
                .Where(x => x.j.MechanicId == mechanicId && x.j.Status == "Completed")
                .OrderByDescending(x => x.j.CompletedAt)
                .Select(x => new {
                    jobId = x.j.Id,
                    customerName = x.j.Customer != null ? x.j.Customer.Name : "Guest",
                    problemType = x.j.ProblemType,
                    completedAt = x.j.CompletedAt.HasValue ? x.j.CompletedAt.Value.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt") : "-",
                    visitingCharge = x.j.VisitingCharge,
                    serviceCharge = x.j.ServiceChargeMin,
                    customEstimateAmount = x.j.CustomEstimateAmount,
                    customEstimateDetails = x.j.CustomEstimateDetails,
                    extraLabour = x.j.ExtraLabourCharge,
                    partsBilled = x.j.PartsEstimateAmount,
                    partsMrp = x.j.PartsMrp,
                    towingCharge = x.j.TowingCharge,
                    totalBill = x.j.FinalBillAmount,
                    adminCommission = x.payment != null ? x.payment.AdminCommissionAmount : 0.0,
                    netCredit = x.payment != null ? x.payment.MechanicEarningAmount : x.j.FinalBillAmount,
                    isCash = x.payment == null || (x.payment.RazorpayPaymentId != null && x.payment.RazorpayPaymentId.StartsWith("pay_cash_"))
                })
                .ToListAsync();

            return Json(new { 
                success = true, 
                jobs = jobs,
                walletBalance = walletBalance,
                pendingPayouts = pendingPayouts,
                settledPayouts = settledPayouts
            });
        }

        [HttpPost]
        public async Task<IActionResult> ReleaseMechanicWalletDirect(int mechanicId, string remarks)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == mechanicId);
            if (profile == null || profile.CurrentEarnings <= 0)
            {
                TempData["Error"] = "Mechanic has no earnings to release.";
                return RedirectToAction("Payments");
            }

            double releaseAmount = profile.CurrentEarnings;

            var req = new MechanicPayoutRequest
            {
                MechanicId = mechanicId,
                Amount = releaseAmount,
                PayoutMethod = string.IsNullOrEmpty(profile.PreferredPayoutMethod) ? "Bank" : profile.PreferredPayoutMethod,
                AccountHolderName = profile.AccountHolderName ?? string.Empty,
                BankAccountNumber = profile.BankAccountNumber ?? string.Empty,
                BankName = profile.BankName ?? string.Empty,
                IfscCode = profile.IfscCode ?? string.Empty,
                UpiId = profile.UpiId ?? string.Empty,
                Status = "Approved",
                CreatedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                TransactionReference = "DIR-" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                AdminRemarks = string.IsNullOrEmpty(remarks) ? "Direct wallet balance release by Admin" : remarks
            };
            _dbContext.MechanicPayoutRequests.Add(req);

            profile.CurrentEarnings = 0.0;

            var adminWithdrawal = new AdminWithdrawal
            {
                Amount = releaseAmount,
                PayoutMethod = req.PayoutMethod == "UPI" ? "UPI Direct" : "Bank Transfer",
                ReferenceNumber = req.TransactionReference,
                WithdrawnAt = DateTime.UtcNow
            };
            _dbContext.AdminWithdrawals.Add(adminWithdrawal);

            var supportMsg = new MechanicSupportMessage
            {
                MechanicId = mechanicId,
                Title = "💰 Wallet Balance Released",
                MessageText = $"Admin has directly released your full wallet balance of ₹{releaseAmount:N2}.\nReference Number: {req.TransactionReference}\nRemarks: {req.AdminRemarks}",
                SenderRole = "Admin",
                SenderName = "RaahSathi Finance Desk",
                IsFromAdmin = true,
                IsRead = false,
                SentAt = DateTime.UtcNow
            };
            _dbContext.MechanicSupportMessages.Add(supportMsg);

            await _dbContext.SaveChangesAsync();

            TempData["Success"] = $"Wallet balance of ₹{releaseAmount:N2} released directly for mechanic!";
            return RedirectToAction("Payments");
        }

        [HttpPost]
        public async Task<IActionResult> SendPayoutDetailsRequest(int mechanicId)
        {
            if (!IsAdmin()) return Unauthorized();

            var supportMsg = new MechanicSupportMessage
            {
                MechanicId = mechanicId,
                Title = "⚠️ Action Required: Submit Payout Details",
                MessageText = "Hi, Admin has requested you to submit your bank account or UPI details under the Settings tab so that your wallet earnings can be released. Please fill them out as soon as possible.",
                SenderRole = "Admin",
                SenderName = "RaahSathi Finance Desk",
                IsFromAdmin = true,
                IsRead = false,
                SentAt = DateTime.UtcNow
            };

            _dbContext.MechanicSupportMessages.Add(supportMsg);
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Request sent to mechanic successfully!" });
        }

        public async Task<IActionResult> Reports()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            ViewBag.TotalJobsCount = await _dbContext.Jobs.CountAsync();
            ViewBag.CompletedJobsCount = await _dbContext.Jobs.CountAsync(j => j.Status == "Completed");
            ViewBag.CancelledJobsCount = await _dbContext.Jobs.CountAsync(j => j.Status == "Cancelled");
            ViewBag.TotalRevenue = await _dbContext.Payments.Where(p => p.PaymentStatus == "Released").SumAsync(p => (double?)p.Amount) ?? 0.0;
            ViewBag.TotalCustomersCount = await _dbContext.Users.CountAsync(u => u.Role == "Customer");
            ViewBag.TotalMechanicsCount = await _dbContext.Users.CountAsync(u => u.Role == "Mechanic");

            return View();
        }

        public async Task<IActionResult> Notifications()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var notifications = await _dbContext.PushNotificationLogs.OrderByDescending(n => n.Id).ToListAsync();
            return View(notifications);
        }

        [HttpPost]
        public async Task<IActionResult> SendPushNotification(string targetAudience, string selectedCity, string title, string message)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var notif = new PushNotificationLog
            {
                TargetAudience = targetAudience ?? "All Users",
                SelectedCity = selectedCity ?? "All",
                Title = title,
                Message = message,
                SentCount = new Random().Next(120, 850),
                SentAt = DateTime.UtcNow
            };
            _dbContext.PushNotificationLogs.Add(notif);
            await _dbContext.SaveChangesAsync();
            await LogAdminActionAsync("PUSH_NOTIFICATION", $"Broadcasted Push Notification to '{targetAudience}': {title}");

            TempData["Success"] = $"Push notification '{title}' broadcasted successfully to {notif.SentCount} devices!";
            return RedirectToAction("Notifications");
        }

        public async Task<IActionResult> Cms()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var banners = await _dbContext.CmsBanners.OrderByDescending(b => b.Id).ToListAsync();
            return View(banners);
        }

        [HttpPost]
        public async Task<IActionResult> AddCmsBanner(string title, string imageUrl, string targetPage)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var banner = new CmsBanner
            {
                Title = title,
                ImageUrl = imageUrl,
                TargetPage = string.IsNullOrWhiteSpace(targetPage) ? "Homepage" : targetPage,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.CmsBanners.Add(banner);
            await _dbContext.SaveChangesAsync();
            await LogAdminActionAsync("CMS_BANNER", $"Added CMS banner: {title}");

            TempData["Success"] = $"Homepage banner '{title}' updated.";
            return RedirectToAction("Cms");
        }

        public async Task<IActionResult> Settings()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            ViewBag.Settings = await _dbContext.AdminSystemSettings.ToListAsync();
            return View();
        }

        public async Task<IActionResult> Account()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var accountsSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminAccountsJson");
            List<AdminAccountModel> accounts = new List<AdminAccountModel>();
            if (accountsSetting != null && !string.IsNullOrEmpty(accountsSetting.SettingValue))
            {
                try
                {
                    accounts = System.Text.Json.JsonSerializer.Deserialize<List<AdminAccountModel>>(accountsSetting.SettingValue) ?? new List<AdminAccountModel>();
                }
                catch
                {
                    accounts = new List<AdminAccountModel>();
                }
            }
            else
            {
                var upi = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminUpiId");
                if (upi != null && !string.IsNullOrEmpty(upi.SettingValue))
                {
                    var holder = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminAccountHolderName");
                    var bank = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminBankName");
                    var num = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminAccountNumber");
                    var ifsc = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminIfscCode");

                    accounts.Add(new AdminAccountModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        UpiId = upi.SettingValue,
                        HolderName = holder?.SettingValue ?? "",
                        BankName = bank?.SettingValue ?? "",
                        AccountNumber = num?.SettingValue ?? "",
                        IfscCode = ifsc?.SettingValue ?? "",
                        IsActive = true
                    });

                    string json = System.Text.Json.JsonSerializer.Serialize(accounts);
                    await SaveOrUpdateSettingAsync("AdminAccountsJson", json, "Account");
                }
            }

            ViewBag.Accounts = accounts;

            var releasedPayments = await _dbContext.Payments.Where(p => p.PaymentStatus == "Released").ToListAsync();
            double rate1 = (await GetSettingDoubleAsync("CommissionPhase1", 8)) / 100.0;
            double rate2 = (await GetSettingDoubleAsync("CommissionPhase2", 10)) / 100.0;
            double rate3 = (await GetSettingDoubleAsync("CommissionPhase3", 12)) / 100.0;
            double totalCommissionEarned = releasedPayments.Sum(p => p.AdminCommissionAmount > 0 
                ? p.AdminCommissionAmount 
                : (p.Amount < 1000 ? p.Amount * rate1 : (p.Amount <= 3000 ? p.Amount * rate2 : p.Amount * rate3)));
            double totalWithdrawn = await _dbContext.AdminWithdrawals.SumAsync(w => (double?)w.Amount) ?? 0.0;
            double adminVaultBalance = Math.Max(0.0, Math.Round(totalCommissionEarned - totalWithdrawn, 2));
            var withdrawalHistory = await _dbContext.AdminWithdrawals.OrderByDescending(w => w.WithdrawnAt).Take(10).ToListAsync();

            ViewBag.TotalCommissionEarned = totalCommissionEarned;
            ViewBag.TotalWithdrawn = totalWithdrawn;
            ViewBag.AdminVaultBalance = adminVaultBalance;
            ViewBag.WithdrawalHistory = withdrawalHistory;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddAdminAccount(string upiId, string holderName, string bankName, string accountNumber, string ifscCode, bool makeActive)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var accountsSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminAccountsJson");
            List<AdminAccountModel> accounts = new List<AdminAccountModel>();
            if (accountsSetting != null && !string.IsNullOrEmpty(accountsSetting.SettingValue))
            {
                try
                {
                    accounts = System.Text.Json.JsonSerializer.Deserialize<List<AdminAccountModel>>(accountsSetting.SettingValue) ?? new List<AdminAccountModel>();
                }
                catch
                {
                    accounts = new List<AdminAccountModel>();
                }
            }

            var newAccount = new AdminAccountModel
            {
                Id = Guid.NewGuid().ToString(),
                UpiId = upiId ?? "",
                HolderName = holderName ?? "",
                BankName = bankName ?? "",
                AccountNumber = accountNumber ?? "",
                IfscCode = ifscCode ?? "",
                IsActive = makeActive || accounts.Count == 0
            };

            if (newAccount.IsActive)
            {
                foreach (var acc in accounts)
                {
                    acc.IsActive = false;
                }
            }

            accounts.Add(newAccount);

            string json = System.Text.Json.JsonSerializer.Serialize(accounts);
            await SaveOrUpdateSettingAsync("AdminAccountsJson", json, "Account");

            if (newAccount.IsActive)
            {
                await MirrorActiveAccountSettings(newAccount);
            }

            await LogAdminActionAsync("ADMIN_ADD_ACCOUNT", $"Added new settlement account: {newAccount.HolderName} ({newAccount.UpiId})");
            TempData["Success"] = "New settlement account added successfully.";
            return RedirectToAction("Account");
        }

        [HttpPost]
        public async Task<IActionResult> ActivateAdminAccount(string accountId)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Not authenticated" });

            var accountsSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminAccountsJson");
            if (accountsSetting == null || string.IsNullOrEmpty(accountsSetting.SettingValue))
                return Json(new { success = false, message = "No accounts configured." });

            List<AdminAccountModel> accounts;
            try
            {
                accounts = System.Text.Json.JsonSerializer.Deserialize<List<AdminAccountModel>>(accountsSetting.SettingValue) ?? new List<AdminAccountModel>();
            }
            catch
            {
                return Json(new { success = false, message = "Failed to parse accounts list." });
            }

            var targetAcc = accounts.FirstOrDefault(a => a.Id == accountId);
            if (targetAcc == null) return Json(new { success = false, message = "Account not found." });

            foreach (var acc in accounts)
            {
                acc.IsActive = (acc.Id == accountId);
            }

            string json = System.Text.Json.JsonSerializer.Serialize(accounts);
            await SaveOrUpdateSettingAsync("AdminAccountsJson", json, "Account");

            await MirrorActiveAccountSettings(targetAcc);

            await LogAdminActionAsync("ADMIN_ACTIVATE_ACCOUNT", $"Activated settlement account: {targetAcc.HolderName} ({targetAcc.UpiId})");
            return Json(new { success = true, message = $"Account '{targetAcc.HolderName}' activated successfully!" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAdminAccount(string accountId)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            var accountsSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminAccountsJson");
            if (accountsSetting == null || string.IsNullOrEmpty(accountsSetting.SettingValue))
                return RedirectToAction("Account");

            List<AdminAccountModel> accounts;
            try
            {
                accounts = System.Text.Json.JsonSerializer.Deserialize<List<AdminAccountModel>>(accountsSetting.SettingValue) ?? new List<AdminAccountModel>();
            }
            catch
            {
                return RedirectToAction("Account");
            }

            var targetAcc = accounts.FirstOrDefault(a => a.Id == accountId);
            if (targetAcc == null) return RedirectToAction("Account");

            bool wasActive = targetAcc.IsActive;
            accounts.Remove(targetAcc);

            if (wasActive && accounts.Count > 0)
            {
                accounts[0].IsActive = true;
                await MirrorActiveAccountSettings(accounts[0]);
            }
            else if (accounts.Count == 0)
            {
                await MirrorActiveAccountSettings(new AdminAccountModel());
            }

            string json = System.Text.Json.JsonSerializer.Serialize(accounts);
            await SaveOrUpdateSettingAsync("AdminAccountsJson", json, "Account");

            await LogAdminActionAsync("ADMIN_DELETE_ACCOUNT", $"Deleted settlement account: {targetAcc.HolderName}");
            TempData["Success"] = "Settlement account deleted successfully.";
            return RedirectToAction("Account");
        }

        private async Task MirrorActiveAccountSettings(AdminAccountModel acc)
        {
            await SaveOrUpdateSettingAsync("AdminUpiId", acc.UpiId ?? "", "Account");
            await SaveOrUpdateSettingAsync("AdminAccountHolderName", acc.HolderName ?? "", "Account");
            await SaveOrUpdateSettingAsync("AdminBankName", acc.BankName ?? "", "Account");
            await SaveOrUpdateSettingAsync("AdminAccountNumber", acc.AccountNumber ?? "", "Account");
            await SaveOrUpdateSettingAsync("AdminIfscCode", acc.IfscCode ?? "", "Account");
        }

        [HttpPost]
        public async Task<IActionResult> SaveSystemSettings(string commissionTierJson, string smsApiKey, string emailSender, string whatsappNo, string googleMapsKey)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            await LogAdminActionAsync("SYSTEM_SETTINGS", "Updated Admin System Settings & Tiered Commission Rules");
            TempData["Success"] = "System API & Commission Settings saved successfully.";
            return RedirectToAction("Settings");
        }

        public async Task<IActionResult> Admins()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var admins = await _dbContext.Users.Where(u => u.Role == "Admin").ToListAsync();
            return View(admins);
        }

        [HttpPost]
        public async Task<IActionResult> AddAdminUser(string name, string phoneNumber, string adminRole)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var adminUser = new User
            {
                Name = name,
                PhoneNumber = phoneNumber,
                Role = "Admin",
                AdminRole = string.IsNullOrWhiteSpace(adminRole) ? "Operations" : adminRole,
                Password = "1234"
            };
            _dbContext.Users.Add(adminUser);
            await _dbContext.SaveChangesAsync();
            await LogAdminActionAsync("ADD_ADMIN", $"Added Admin User: {name} (Role: {adminRole})");

            TempData["Success"] = $"Admin User '{name}' ({adminRole}) created.";
            return RedirectToAction("Admins");
        }

        public async Task<IActionResult> Logs()
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");
            var logs = await _dbContext.AuditLogs.OrderByDescending(l => l.Id).Take(200).ToListAsync();
            return View(logs);
        }

        [HttpGet("/Admin/GetLiveTelemetry")]
        public async Task<IActionResult> GetLiveTelemetry()
        {
            DateTime today = DateTime.Today;

            int onlineMechanics = await _dbContext.MechanicProfiles.CountAsync(m => m.IsOnline);
            int liveRequests = await _dbContext.Jobs.CountAsync(j => j.Status == "Requested");
            int activeJobs = await _dbContext.Jobs.CountAsync(j => j.Status != "Completed" && j.Status != "Cancelled");
            int pendingRequests = await _dbContext.Jobs.CountAsync(j => j.Status == "Requested" && j.MechanicId == null);
            
            int completedToday = await _dbContext.Jobs.CountAsync(j => j.Status == "Completed" && (j.CompletedAt >= today || j.CreatedAt >= today));
            if (completedToday == 0)
            {
                completedToday = await _dbContext.Jobs.CountAsync(j => j.Status == "Completed");
            }

            int cancelledToday = await _dbContext.Jobs.CountAsync(j => j.Status == "Cancelled" && (j.CompletedAt >= today || j.CreatedAt >= today));
            if (cancelledToday == 0)
            {
                cancelledToday = await _dbContext.Jobs.CountAsync(j => j.Status == "Cancelled");
            }

            var todayPayments = await _dbContext.Payments.Where(p => p.PaymentStatus == "Released" && p.CreatedAt >= today).ToListAsync();
            double todayRevenue = todayPayments.Sum(p => p.Amount);
            double rate1 = (await GetSettingDoubleAsync("CommissionPhase1", 8)) / 100.0;
            double rate2 = (await GetSettingDoubleAsync("CommissionPhase2", 10)) / 100.0;
            double rate3 = (await GetSettingDoubleAsync("CommissionPhase3", 12)) / 100.0;
            double todayCommission = todayPayments.Sum(p => p.AdminCommissionAmount > 0 
                ? p.AdminCommissionAmount 
                : (p.Amount < 1000 ? p.Amount * rate1 : (p.Amount <= 3000 ? p.Amount * rate2 : p.Amount * rate3)));

            var ratedJobs = await _dbContext.Jobs.Where(j => j.RatingFromCustomer.HasValue).Select(j => j.RatingFromCustomer.GetValueOrDefault()).ToListAsync();
            double avgRating = ratedJobs.Count > 0 ? Math.Round(ratedJobs.Average(), 1) : 4.8;

            var emergencySetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "EmergencyMode");
            bool isEmergencyMode = emergencySetting?.SettingValue?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

            var activeDisputesCount = await _dbContext.Jobs.CountAsync(j => j.DisputeStatus == "Active");
            var pendingKycCount = await _dbContext.MechanicProfiles.CountAsync(m => m.KycStatus == "Pending");

            var recentJobs = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Include(j => j.Vehicle)
                .OrderByDescending(j => j.Id)
                .Take(10)
                .Select(j => new {
                    j.Id,
                    DisplayId = $"#RS-{j.Id}",
                    CustomerName = j.Customer != null ? j.Customer.Name : "Customer",
                    CustomerPhone = j.Customer != null ? j.Customer.PhoneNumber : "",
                    MechanicName = j.Mechanic != null ? j.Mechanic.Name : "Matching (15s)...",
                    VehicleModel = j.Vehicle != null ? j.Vehicle.Model : "Vehicle",
                    RegNo = j.Vehicle != null ? j.Vehicle.RegistrationNumber : "",
                    j.ProblemType,
                    j.Status,
                    j.FinalBillAmount,
                    j.CustomerLat,
                    j.CustomerLng,
                    CreatedAt = j.CreatedAt.ToString("HH:mm:ss")
                })
                .ToListAsync();

            var cities = await _dbContext.CityServiceAreas.Select(c => new {
                c.Id,
                c.State,
                c.CityName,
                c.AreaName,
                c.ServiceRadiusKm
            }).ToListAsync();

            return Json(new {
                success = true,
                onlineMechanics,
                liveRequests,
                activeJobs,
                pendingRequests,
                completedToday,
                cancelledToday,
                todayRevenue,
                todayCommission,
                avgResponseTimeMinutes = 8,
                avgRating,
                isEmergencyMode,
                activeDisputesCount,
                pendingKycCount,
                recentJobs,
                cities
            });
        }

        [HttpGet("/Admin/GetTelemetryCategoryDetails")]
        public async Task<IActionResult> GetTelemetryCategoryDetails(string category)
        {
            DateTime today = DateTime.Today;

            if (category == "onlineMechanics")
            {
                var mechanics = await _dbContext.MechanicProfiles
                    .Include(m => m.User)
                    .Where(m => m.IsOnline)
                    .OrderByDescending(m => m.Rating)
                    .Select(m => new {
                        Type = "Mechanic",
                        Id = m.UserId,
                        DisplayId = $"#MCH-{m.UserId}",
                        Name = m.User != null ? m.User.Name : "Mechanic",
                        Phone = m.User != null ? m.User.PhoneNumber : "",
                        SubDetail = m.GarageName ?? m.ShopName ?? "Individual Mechanic",
                        Skill = m.SkillCategory ?? "General",
                        Rating = m.Rating > 0 ? $"{m.Rating} ★" : "5.0 ★",
                        Status = "🟢 Online",
                        Extra = $"{m.ServiceRadiusKm} KM Radius | {m.TotalJobs} Jobs Done"
                    })
                    .ToListAsync();

                return Json(new { success = true, category, title = "Online Mechanics Network", type = "Mechanic", items = mechanics });
            }

            IQueryable<Job> query = _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Include(j => j.Vehicle);

            string title = "Telemetry Category Details";

            if (category == "liveRequests")
            {
                query = query.Where(j => j.Status == "Requested");
                title = "Live Assistance Requests";
            }
            else if (category == "activeJobs")
            {
                query = query.Where(j => j.Status != "Completed" && j.Status != "Cancelled");
                title = "Active Jobs in Progress";
            }
            else if (category == "pendingRequests")
            {
                query = query.Where(j => j.Status == "Requested" && j.MechanicId == null);
                title = "Unassigned Pending Requests";
            }
            else if (category == "completedToday")
            {
                var countToday = await _dbContext.Jobs.CountAsync(j => j.Status == "Completed" && (j.CompletedAt >= today || j.CreatedAt >= today));
                if (countToday > 0)
                {
                    query = query.Where(j => j.Status == "Completed" && (j.CompletedAt >= today || j.CreatedAt >= today));
                }
                else
                {
                    query = query.Where(j => j.Status == "Completed");
                }
                title = "Completed Assistance Jobs";
            }
            else if (category == "cancelledToday")
            {
                var countToday = await _dbContext.Jobs.CountAsync(j => j.Status == "Cancelled" && (j.CompletedAt >= today || j.CreatedAt >= today));
                if (countToday > 0)
                {
                    query = query.Where(j => j.Status == "Cancelled" && (j.CompletedAt >= today || j.CreatedAt >= today));
                }
                else
                {
                    query = query.Where(j => j.Status == "Cancelled");
                }
                title = "Cancelled Assistance Jobs";
            }

            var jobs = await query
                .OrderByDescending(j => j.Id)
                .Select(j => new {
                    Type = "Job",
                    Id = j.Id,
                    DisplayId = $"#RS-{j.Id}",
                    CustomerName = j.Customer != null ? j.Customer.Name : "Customer",
                    CustomerPhone = j.Customer != null ? j.Customer.PhoneNumber : "",
                    MechanicName = j.Mechanic != null ? j.Mechanic.Name : "Unassigned / Auto-Matching",
                    MechanicPhone = j.Mechanic != null ? j.Mechanic.PhoneNumber : "",
                    VehicleModel = j.Vehicle != null ? j.Vehicle.Model : "Vehicle",
                    RegNo = j.Vehicle != null ? j.Vehicle.RegistrationNumber : "",
                    ProblemType = j.ProblemType ?? "Roadside Assistance",
                    Address = j.Address ?? "GPS Location",
                    Status = j.Status,
                    Amount = j.FinalBillAmount > 0 ? $"₹{j.FinalBillAmount}" : "₹350 (Est.)",
                    CreatedAt = j.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                })
                .ToListAsync();

            return Json(new { success = true, category, title, type = "Job", items = jobs });
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePricingRule(int ruleId, string cityName, double baseFee, double perKmRate, double baseTowingFee, double perKmTowingRate)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Unauthorized access." });

            bool success = await _pricingService.UpdateCategoryBaseRatesAsync(ruleId, cityName, baseFee, perKmRate, baseTowingFee, perKmTowingRate);
            if (!success) return Json(new { success = false, message = "Invalid pricing rule values." });

            await LogAdminActionAsync("UPDATE_BASE_PRICING", $"Updated Rule ID {ruleId} ({cityName}) -> Base: ₹{baseFee}, PerKM: ₹{perKmRate}, BaseTowing: ₹{baseTowingFee}, PerKmTowing: ₹{perKmTowingRate}");

            string referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer) && !Request.Headers["X-Requested-With"].ToString().Equals("XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Success"] = "Pricing Engine Tuning rates updated successfully!";
                return Redirect(referer);
            }

            return Json(new { success = true, message = "Pricing Rule updated successfully." });
        }

        [HttpPost]
        public async Task<IActionResult> SaveCommissionSettings(int phase1, int phase2, int phase3, int parts)
        {
            if (!IsAdmin()) return RedirectToAction("Login", "Auth");

            await SaveOrUpdateSettingAsync("CommissionPhase1", phase1.ToString(), "Commission");
            await SaveOrUpdateSettingAsync("CommissionPhase2", phase2.ToString(), "Commission");
            await SaveOrUpdateSettingAsync("CommissionPhase3", phase3.ToString(), "Commission");
            await SaveOrUpdateSettingAsync("CommissionParts", parts.ToString(), "Commission");

            await LogAdminActionAsync("COMMISSION_SETTINGS", $"Updated commission phases: P1={phase1}%, P2={phase2}%, P3={phase3}%, Parts={parts}%");
            
            TempData["Success"] = "Admin commission rates updated successfully!";
            return RedirectToAction("Pricing");
        }

        private async Task SaveOrUpdateSettingAsync(string key, string value, string category)
        {
            var setting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
            if (setting == null)
            {
                setting = new AdminSystemSetting
                {
                    SettingKey = key,
                    SettingValue = value,
                    Category = category,
                    Description = $"Admin commission rate config for {key}"
                };
                _dbContext.AdminSystemSettings.Add(setting);
            }
            else
            {
                setting.SettingValue = value;
                _dbContext.Entry(setting).State = EntityState.Modified;
            }
            await _dbContext.SaveChangesAsync();
        }

        private async Task<double> GetSettingDoubleAsync(string key, double defaultValue)
        {
            try
            {
                var setting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
                if (setting != null && double.TryParse(setting.SettingValue, out double val))
                {
                    return val;
                }
            }
            catch { }
            return defaultValue;
        }

        private async Task<int> GetSettingIntAsync(string key, int defaultValue)
        {
            try
            {
                var setting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key);
                if (setting != null && int.TryParse(setting.SettingValue, out int val))
                {
                    return val;
                }
            }
            catch { }
            return defaultValue;
        }

        [HttpGet("/Admin/GlobalSearch")]
        public async Task<IActionResult> GlobalSearch(string query)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Not authenticated" });
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return Json(new { success = true, mechanics = new List<object>(), jobs = new List<object>() });
            }

            string cleanQuery = query.Trim();

            // Search mechanics
            var matchingMechanics = await _dbContext.MechanicProfiles
                .Include(m => m.User)
                .Where(m => m.User != null && (m.User.Name.Contains(cleanQuery) || m.User.PhoneNumber.Contains(cleanQuery) || m.ShopName.Contains(cleanQuery) || m.DisplayId.Contains(cleanQuery)))
                .Take(5)
                .Select(m => new {
                    id = m.UserId,
                    displayId = m.DisplayId,
                    name = m.User != null ? m.User.Name : "Mechanic",
                    phone = m.User != null ? m.User.PhoneNumber : "",
                    shopName = m.ShopName,
                    rating = m.Rating,
                    experience = m.ExperienceYears,
                    isOnline = m.IsOnline,
                    kycStatus = m.KycStatus
                })
                .ToListAsync();

            // Search jobs
            int targetJobId = -1;
            bool isInt = int.TryParse(cleanQuery.Replace("#", "").Replace("RS-", "").Replace("rs-", ""), out targetJobId);

            var matchingJobs = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Where(j => (isInt && j.Id == targetJobId) || 
                            (j.Customer != null && j.Customer.Name.Contains(cleanQuery)) || 
                            (j.Mechanic != null && j.Mechanic.Name.Contains(cleanQuery)) || 
                            j.ProblemType.Contains(cleanQuery) || 
                            j.Status.Contains(cleanQuery))
                .OrderByDescending(j => j.Id)
                .Take(5)
                .Select(j => new {
                    id = j.Id,
                    customerName = j.Customer != null ? j.Customer.Name : "Customer",
                    mechanicName = j.Mechanic != null ? j.Mechanic.Name : "Matching...",
                    problem = j.ProblemType,
                    status = j.Status,
                    finalBill = j.FinalBillAmount
                })
                .ToListAsync();

            return Json(new {
                success = true,
                mechanics = matchingMechanics,
                jobs = matchingJobs
            });
        }

        [HttpGet("/Admin/GetGlobalJobDetails")]
        public async Task<IActionResult> GetGlobalJobDetails(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Not authenticated" });

            var job = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .Include(j => j.Vehicle)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null) return Json(new { success = false, message = "Job not found." });

            return Json(new {
                success = true,
                id = job.Id,
                displayId = $"#RS-{job.Id:D4}",
                customerName = job.Customer?.Name ?? "Customer",
                customerPhone = job.Customer?.PhoneNumber ?? "N/A",
                mechanicName = job.Mechanic?.Name ?? "Pending Matching",
                mechanicPhone = job.Mechanic?.PhoneNumber ?? "N/A",
                vehicle = $"{job.Vehicle?.Model ?? "Vehicle"} ({job.Vehicle?.RegistrationNumber ?? "N/A"})",
                problem = job.ProblemType,
                description = job.ProblemDescription,
                status = job.Status,
                visiting = job.VisitingCharge,
                service = job.ServiceChargeMin,
                customEst = job.CustomEstimateAmount,
                parts = job.PartsEstimateAmount,
                labour = job.ExtraLabourCharge,
                towing = job.TowingCharge,
                total = job.FinalBillAmount > 0 ? job.FinalBillAmount : (job.VisitingCharge + job.ServiceChargeMin),
                date = job.CreatedAt.ToString("dd MMM yyyy, hh:mm tt"),
                completedDate = job.CompletedAt?.ToString("dd MMM yyyy, hh:mm tt") ?? "N/A",
                address = job.Address,
                disputeStatus = job.DisputeStatus,
                disputeReason = job.DisputeReason
            });
        }

        [HttpGet("/Admin/GetGlobalMechanicDetails")]
        public async Task<IActionResult> GetGlobalMechanicDetails(int id)
        {
            if (!IsAdmin()) return Json(new { success = false, message = "Not authenticated" });

            var mechanic = await _dbContext.MechanicProfiles
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.UserId == id);

            if (mechanic == null) return Json(new { success = false, message = "Mechanic not found." });

            return Json(new {
                success = true,
                id = mechanic.UserId,
                displayId = mechanic.DisplayId,
                name = mechanic.User?.Name ?? "Mechanic",
                phone = mechanic.User?.PhoneNumber ?? "N/A",
                shopName = mechanic.ShopName,
                shopAddress = mechanic.ShopAddress,
                shopTimings = mechanic.ShopTiming,
                rating = mechanic.Rating,
                experience = mechanic.ExperienceYears,
                specializations = mechanic.Specialization,
                vehicles = mechanic.VehicleExpertise,
                kycStatus = mechanic.KycStatus,
                isOnline = mechanic.IsOnline,
                aadhaar = mechanic.AadhaarNumber,
                earnings = mechanic.CurrentEarnings,
                totalJobs = mechanic.TotalJobs,
                certification = mechanic.IsCertified ? "Certified Professional" : "Standard Partner"
            });
        }
    }

    public class AdminAccountModel
    {
        public string Id { get; set; } = string.Empty;
        public string UpiId { get; set; } = string.Empty;
        public string HolderName { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string IfscCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
