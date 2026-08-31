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
    public class MechanicController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
        private readonly Services.IDispatchEngine _dispatchEngine;
        private readonly Services.IPaymentService _paymentService;
        private readonly Services.IJobService _jobService;
        private readonly Services.IWalletService _walletService;
        private readonly Services.IUserService _userService;
        private readonly Services.IReferralService _referralService;
        private readonly Services.IPricingEngine _pricingEngine;

        public MechanicController(
            ApplicationDbContext dbContext,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
            Services.IDispatchEngine dispatchEngine,
            Services.IPaymentService paymentService,
            Services.IJobService jobService,
            Services.IWalletService walletService,
            Services.IUserService userService,
            Services.IReferralService referralService,
            Services.IPricingEngine pricingEngine)
        {
            _dbContext = dbContext;
            _env = env;
            _dispatchEngine = dispatchEngine;
            _paymentService = paymentService;
            _jobService = jobService;
            _walletService = walletService;
            _userService = userService;
            _referralService = referralService;
            _pricingEngine = pricingEngine;
        }

        private async Task<User?> GetActiveMechanicUserAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int mechId))
                {
                    var user = await _dbContext.Users.FindAsync(mechId);
                    if (user != null)
                    {
                        if (user.Role != "Mechanic" && user.Role != "Admin")
                        {
                            user.Role = "Mechanic";
                            await _dbContext.SaveChangesAsync();
                        }
                        return user;
                    }
                }
            }

            return null;
        }

        private bool OtherMechanicAvailable(Job job, double maxRadiusKm, int currentMechanicId)
        {
            var busyMechanicIds = _dbContext.Jobs.AsNoTracking()
                .Where(j => j.MechanicId != null && j.Status != "Completed" && j.Status != "Cancelled")
                .Select(j => j.MechanicId!.Value)
                .ToHashSet();

            var onlineProfiles = _dbContext.MechanicProfiles.AsNoTracking()
                .Where(p => p.UserId != currentMechanicId && p.IsOnline && p.KycStatus == "Approved")
                .ToList();

            foreach (var profile in onlineProfiles)
            {
                // Check active job in memory from pre-fetched set
                if (busyMechanicIds.Contains(profile.UserId))
                    continue;

                // Check distance
                double dist = _dispatchEngine.CalculateDistance(job.CustomerLat, job.CustomerLng, profile.Latitude, profile.Longitude);
                double allowedRadius = Math.Max(maxRadiusKm, profile.ServiceRadiusKm);
                if (dist > allowedRadius)
                    continue;

                // Check if they declined/snoozed this job
                if (!string.IsNullOrEmpty(job.DeclinedMechanicIds))
                {
                    var otherStrId = profile.UserId.ToString();
                    var entries = job.DeclinedMechanicIds.Split(',').Select(id => id.Trim()).ToList();
                    bool otherDeclined = false;
                    foreach (var entry in entries)
                    {
                        if (entry == otherStrId)
                        {
                            otherDeclined = true;
                            break;
                        }
                        if (entry.StartsWith(otherStrId + "_snooze_"))
                        {
                            string tsStr = entry.Substring((otherStrId + "_snooze_").Length);
                            if (DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime snoozeUntil))
                            {
                                if (DateTime.UtcNow < snoozeUntil)
                                {
                                    otherDeclined = true;
                                    break;
                                }
                            }
                        }
                        if (entry.StartsWith(otherStrId + "_decline_"))
                        {
                            string tsStr = entry.Substring((otherStrId + "_decline_").Length);
                            if (DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime declineUntil))
                            {
                                if (DateTime.UtcNow < declineUntil)
                                {
                                    otherDeclined = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (otherDeclined)
                        continue;
                }

                return true; // Found another viable mechanic
            }

            return false;
        }

        public async Task<IActionResult> Dashboard()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return RedirectToAction("Login", "Auth", new { role = "Mechanic" });

            var profile = await _dbContext.MechanicProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (profile == null)
            {
                // Create a basic incomplete profile if none exists
                profile = new MechanicProfile
                {
                    UserId = user.Id,
                    KycStatus = "Incomplete",
                };
                _dbContext.MechanicProfiles.Add(profile);
                await _dbContext.SaveChangesAsync();
            }

            if (profile.KycStatus == "Incomplete" || profile.KycStatus == "Pending")
            {
                return RedirectToAction("KycForm");
            }

            // Find current active job
            var activeJob = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .FirstOrDefaultAsync(j => j.MechanicId == user.Id && j.Status != "Completed" && j.Status != "Cancelled");

            if (activeJob != null && activeJob.Status == "Arrived")
            {
                activeJob.Status = "Inspecting";
                await _dbContext.SaveChangesAsync();
            }

            // Check if there is an unassigned "Requested" job nearby that fits this mechanic's skills
            // To simulate incoming dispatch pings in the UI
            Job? pingJob = null;
            if (profile.IsOnline && profile.KycStatus == "Approved" && activeJob == null)
            {
                var candidates = await _dbContext.Jobs
                    .Include(j => j.Customer)
                    .Include(j => j.Vehicle)
                    .Where(j => j.Status == "Requested" && j.MechanicId == null)
                    .OrderByDescending(j => j.CreatedAt)
                    .ToListAsync();

                string userStrId = user.Id.ToString();
                foreach (var job in candidates)
                {
                    double jobAgeSeconds = (DateTime.UtcNow - job.CreatedAt).TotalSeconds;
                    if (jobAgeSeconds >= 300)
                    {
                        job.Status = "TimedOut";
                        await _dbContext.SaveChangesAsync();
                        continue;
                    }

                    bool shouldSkip = false;
                    if (!string.IsNullOrEmpty(job.DeclinedMechanicIds))
                    {
                        var entries = job.DeclinedMechanicIds.Split(',').Select(id => id.Trim()).ToList();
                        foreach (var entry in entries)
                        {
                            if (entry == userStrId)
                            {
                                shouldSkip = true; // Permanently declined
                                break;
                            }
                            if (entry.StartsWith(userStrId + "_snooze_"))
                            {
                                string tsStr = entry.Substring((userStrId + "_snooze_").Length);
                                if (DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime snoozeUntil))
                                {
                                    if (DateTime.UtcNow < snoozeUntil)
                                    {
                                        shouldSkip = true; // Still snoozed
                                        break;
                                    }
                                }
                            }
                            if (entry.StartsWith(userStrId + "_decline_"))
                            {
                                string tsStr = entry.Substring((userStrId + "_decline_").Length);
                                if (DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime declineUntil))
                                {
                                    if (DateTime.UtcNow < declineUntil)
                                    {
                                        shouldSkip = true; // Still within 2 minutes of decline
                                        break;
                                    }
                                }
                            }
                            if (entry.StartsWith(userStrId + "_timeout_"))
                            {
                                string countStr = entry.Substring((userStrId + "_timeout_").Length);
                                if (int.TryParse(countStr, out int timeoutCount) && timeoutCount >= 5)
                                {
                                    shouldSkip = true; // Permanently skip after 5 timeouts
                                    break;
                                }
                            }
                            if (entry.StartsWith(userStrId + "_cooldown_"))
                            {
                                string tsStr = entry.Substring((userStrId + "_cooldown_").Length);
                                if (DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime cooldownUntil))
                                {
                                    if (DateTime.UtcNow < cooldownUntil)
                                    {
                                        shouldSkip = true; // Still cooling down
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    if (!shouldSkip)
                    {
                        pingJob = job;
                        break;
                    }
                }
            }

            // Historical Jobs
            var pastJobs = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .Where(j => j.MechanicId == user.Id && (j.Status == "Completed" || j.Status == "Paid" || j.Status == "Closed"))
                .OrderByDescending(j => j.CompletedAt ?? j.CreatedAt)
                .ToListAsync();

            // Active Warning from Admin
            var activeWarning = await _dbContext.MechanicWarnings
                .Include(w => w.Complaint)
                .FirstOrDefaultAsync(w => w.MechanicId == user.Id && !w.IsAcknowledged);

            // Support Team Inbox Messages for this mechanic
            var supportMessages = await _dbContext.MechanicSupportMessages
                .Where(m => m.MechanicId == user.Id)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();

            // Calculate Wallet Stats
            var allPayments = await _dbContext.Payments
                .Include(p => p.Job)
                .Where(p => p.Job != null && p.Job.MechanicId == user.Id)
                .ToListAsync();

            var releasedPayments = allPayments.Where(p => p.PaymentStatus == "Released" || p.PaymentStatus == "Completed" || p.PaymentStatus == "Paid").ToList();
            var heldPayments = allPayments.Where(p => p.PaymentStatus == "Held" || p.PaymentStatus == "Pending").ToList();

            var nowLocal = DateTime.UtcNow.ToLocalTime();
            var todayLocal = nowLocal.Date;

            // Current Week: Monday 00:00 to next Monday 00:00
            int diffToMonday = (7 + (int)todayLocal.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            var startOfWeek = todayLocal.AddDays(-diffToMonday);
            var endOfWeek = startOfWeek.AddDays(7);

            // Current Month: 1st of month 00:00 to 1st of next month 00:00
            var startOfMonth = new DateTime(todayLocal.Year, todayLocal.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            double todayEarnings = releasedPayments
                .Where(p => p.CreatedAt.ToLocalTime().Date == todayLocal)
                .Sum(p => p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount));

            double weeklyEarnings = releasedPayments
                .Where(p => {
                    var d = p.CreatedAt.ToLocalTime().Date;
                    return d >= startOfWeek && d < endOfWeek;
                })
                .Sum(p => p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount));

            double monthlyEarnings = releasedPayments
                .Where(p => {
                    var d = p.CreatedAt.ToLocalTime().Date;
                    return d >= startOfMonth && d < endOfMonth;
                })
                .Sum(p => p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount));

            // Current Month Total Withdrawal (Completed / Approved Payout Requests)
            double monthlyWithdrawals = await _dbContext.MechanicPayoutRequests
                .Where(r => r.MechanicId == user.Id && (r.Status == "Approved" || r.Status == "Completed") && r.CreatedAt >= startOfMonth.ToUniversalTime() && r.CreatedAt < endOfMonth.ToUniversalTime())
                .SumAsync(r => (double?)r.Amount) ?? 0.0;

            double heldEarnings = heldPayments.Sum(p => p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount));

            double pendingSettlement = profile.CurrentEarnings;

            double pendingPayout = await _dbContext.MechanicPayoutRequests
                .Where(r => r.MechanicId == user.Id && r.Status == "Pending")
                .SumAsync(r => r.Amount);

            ViewBag.User = user;
            ViewBag.Profile = profile;
            ViewBag.ActiveJob = activeJob;
            ViewBag.PingJob = pingJob;
            ViewBag.PastJobs = pastJobs;
            ViewBag.ActiveWarning = activeWarning;
            ViewBag.SupportMessages = supportMessages;
            ViewBag.UnreadSupportCount = supportMessages.Count(m => !m.IsRead && m.IsFromAdmin);
            var todayUtc = DateTime.UtcNow.Date;
            var startOfWeekUtc = startOfWeek.ToUniversalTime();
            int todayJobsCount = await _dbContext.Jobs
                .CountAsync(j => j.MechanicId == user.Id && j.Status == "Completed" && (j.CompletedAt ?? j.CreatedAt) >= todayUtc);
            int weeklyJobsCount = await _dbContext.Jobs
                .CountAsync(j => j.MechanicId == user.Id && j.Status == "Completed" && (j.CompletedAt ?? j.CreatedAt) >= startOfWeekUtc);

            var referralSummary = await _referralService.GetUserReferralSummaryAsync(user.Id);

            ViewBag.TodayEarnings = todayEarnings;
            ViewBag.WeeklyEarnings = weeklyEarnings;
            ViewBag.MonthlyEarnings = monthlyEarnings;
            ViewBag.MonthlyVolume = monthlyEarnings;
            ViewBag.MonthlyWithdrawals = monthlyWithdrawals;
            ViewBag.TodayJobsCount = todayJobsCount;
            ViewBag.WeeklyJobsCount = weeklyJobsCount;
            ViewBag.HeldEarnings = heldEarnings;
            ViewBag.PendingSettlement = pendingSettlement;
            ViewBag.PendingPayoutAmount = pendingPayout;
            ViewBag.Payments = allPayments.OrderByDescending(p => p.CreatedAt).ToList();
            ViewBag.ReferralSummary = referralSummary;
            ViewBag.ReferralSettings = await _referralService.GetSettingsAsync();

            return View();
        }

        [HttpGet("/Mechanic/GetReferralData")]
        public async Task<IActionResult> GetReferralData()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Unauthorized();

            var summary = await _referralService.GetUserReferralSummaryAsync(user.Id);
            return Json(new { success = true, summary });
        }

        [HttpPost("/Mechanic/SubmitReferralWithdrawal")]
        public async Task<IActionResult> SubmitReferralWithdrawal(double amount, string payoutMethod, string accountHolder, string bankAccount, string bankName, string ifsc, string upiId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Unauthorized();

            var result = await _referralService.RequestReferralWithdrawalAsync(
                user.Id,
                amount,
                payoutMethod,
                accountHolder,
                bankAccount,
                bankName,
                ifsc,
                upiId
            );

            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> AcknowledgeWarning(int warningId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "User not authenticated." });

            var warning = await _dbContext.MechanicWarnings.FindAsync(warningId);
            if (warning != null && warning.MechanicId == user.Id)
            {
                warning.IsAcknowledged = true;
                await _dbContext.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Warning not found." });
        }

        [HttpGet]
        public async Task<IActionResult> GetKycStatus()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            return Json(new { 
                success = true, 
                kycStatus = profile?.KycStatus ?? "Incomplete",
                isApproved = profile?.KycStatus == "Approved"
            });
        }

        [HttpGet]
        public async Task<IActionResult> KycForm()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return RedirectToAction("Login", "Auth");

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);

            if (profile != null && profile.KycStatus == "Approved")
            {
                return RedirectToAction("Dashboard");
            }

            ViewBag.UserName = user.Name;
            ViewBag.UserPhone = user.PhoneNumber;
            ViewBag.KycStatus = profile?.KycStatus ?? "Incomplete";
            ViewBag.Profile = profile;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitKycForm(
            string Email, DateTime? DateOfBirth, string Gender, IFormFile ProfilePhoto,
            string AadhaarNumber, IFormFile AadhaarFrontPhoto, IFormFile AadhaarBackPhoto, IFormFile DrivingLicencePhoto, IFormFile PanCardPhoto, IFormFile SelfiePhoto,
            string ShopName, string ShopAddress, string Pincode, string ShopTiming, IFormFile ShopPhoto,
            int ExperienceYears, bool IsCertified, string GarageName,
            string[] VehicleExpertise, string[]? ErickshawSkills, string[]? AutoSkills, string[] Specialization, int ServiceRadiusKm)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return RedirectToAction("Login", "Auth");

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null)
            {
                profile = new MechanicProfile { UserId = user.Id };
                _dbContext.MechanicProfiles.Add(profile);
            }

            // Helper to validate files
            bool IsValidDocument(IFormFile f)
            {
                if (f == null || f.Length == 0) return false;
                var ext = System.IO.Path.GetExtension(f.FileName).ToLowerInvariant();
                var allowed = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
                if (!allowed.Contains(ext)) return false;

                var mime = f.ContentType.ToLowerInvariant();
                var allowedMime = new[] { "image/jpeg", "image/jpg", "image/png", "application/pdf" };
                if (!allowedMime.Contains(mime)) return false;

                return true;
            }

            // Document validations
            if ((ProfilePhoto != null && !IsValidDocument(ProfilePhoto)) ||
                (AadhaarFrontPhoto != null && !IsValidDocument(AadhaarFrontPhoto)) ||
                (AadhaarBackPhoto != null && !IsValidDocument(AadhaarBackPhoto)) ||
                (DrivingLicencePhoto != null && !IsValidDocument(DrivingLicencePhoto)) ||
                (PanCardPhoto != null && !IsValidDocument(PanCardPhoto)) ||
                (SelfiePhoto != null && !IsValidDocument(SelfiePhoto)) ||
                (ShopPhoto != null && !IsValidDocument(ShopPhoto)))
            {
                TempData["Error"] = "Invalid document file type. Only JPG, JPEG, PNG and PDF formats are allowed.";
                return RedirectToAction("Dashboard");
            }

            // Helper to save files
            async Task<string> SaveFileAsync(IFormFile file)
            {
                if (file == null || file.Length == 0) return "";
                var uploadsFolder = System.IO.Path.Combine(_env.WebRootPath, "uploads");
                System.IO.Directory.CreateDirectory(uploadsFolder);
                string safeExtension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
                var uniqueName = Guid.NewGuid().ToString("N") + safeExtension;
                var filePath = System.IO.Path.Combine(uploadsFolder, uniqueName);
                using (var stream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                return "/uploads/" + uniqueName;
            }

            profile.Email = Email ?? profile.Email ?? "";
            if (DateOfBirth.HasValue) profile.DateOfBirth = DateOfBirth;
            profile.Gender = Gender ?? profile.Gender ?? "";
            profile.AadhaarNumber = AadhaarNumber ?? profile.AadhaarNumber ?? "";
            profile.ShopName = ShopName ?? profile.ShopName ?? "";
            profile.ShopAddress = ShopAddress ?? profile.ShopAddress ?? "";
            profile.Pincode = Pincode ?? profile.Pincode ?? "";
            profile.ShopTiming = ShopTiming ?? profile.ShopTiming ?? "";
            profile.ExperienceYears = ExperienceYears;
            profile.IsCertified = IsCertified;
            profile.GarageName = GarageName ?? profile.GarageName ?? "";
            profile.ServiceRadiusKm = ServiceRadiusKm;
            profile.VehicleExpertise = VehicleExpertise != null ? string.Join(", ", VehicleExpertise) : (profile.VehicleExpertise ?? "");
            profile.ErickshawSkills = ErickshawSkills != null ? string.Join(", ", ErickshawSkills) : (profile.ErickshawSkills ?? "");
            profile.AutoSkills = AutoSkills != null ? string.Join(", ", AutoSkills) : (profile.AutoSkills ?? "");
            profile.Specialization = Specialization != null ? string.Join(", ", Specialization) : (profile.Specialization ?? "");
            
            // Map legacy SkillCategory based on VehicleExpertise
            profile.SkillCategory = !string.IsNullOrEmpty(profile.VehicleExpertise) ? profile.VehicleExpertise : "Car"; 

            if (ProfilePhoto != null) profile.ProfilePhotoUrl = await SaveFileAsync(ProfilePhoto);
            if (AadhaarFrontPhoto != null) profile.AadhaarFrontUrl = await SaveFileAsync(AadhaarFrontPhoto);
            if (AadhaarBackPhoto != null) profile.AadhaarBackUrl = await SaveFileAsync(AadhaarBackPhoto);
            if (DrivingLicencePhoto != null) profile.DrivingLicenceUrl = await SaveFileAsync(DrivingLicencePhoto);
            if (PanCardPhoto != null) profile.PanCardUrl = await SaveFileAsync(PanCardPhoto);
            if (SelfiePhoto != null) profile.SelfieUrl = await SaveFileAsync(SelfiePhoto);
            if (ShopPhoto != null) profile.ShopPhotoUrl = await SaveFileAsync(ShopPhoto);

            // Ensure no string property is null for SQL Server NOT NULL constraints
            profile.ProfilePhotoUrl ??= "";
            profile.AadhaarFrontUrl ??= "";
            profile.AadhaarBackUrl ??= "";
            profile.DrivingLicenceUrl ??= "";
            profile.PanCardUrl ??= "";
            profile.SelfieUrl ??= "";
            profile.ShopPhotoUrl ??= "";
            profile.Email ??= "";
            profile.Gender ??= "";
            profile.AadhaarNumber ??= "";
            profile.ShopName ??= "";
            profile.ShopAddress ??= "";
            profile.Pincode ??= "";
            profile.ShopTiming ??= "";
            profile.GarageName ??= "";
            profile.VehicleExpertise ??= "";
            profile.ErickshawSkills ??= "";
            profile.AutoSkills ??= "";
            profile.Specialization ??= "";
            profile.SkillCategory ??= "Car";

            profile.KycStatus = "Pending";
            
            await _dbContext.SaveChangesAsync();

            TempData["Success"] = "KYC documents submitted successfully. Please wait for Admin approval.";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleOnline()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false });

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile != null)
            {
                profile.IsOnline = !profile.IsOnline;
                await _dbContext.SaveChangesAsync();
                return Json(new { success = true, isOnline = profile.IsOnline });
            }

            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateLocation(double lat, double lng)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return RedirectToAction("Login", "Auth");

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile != null)
            {
                profile.Latitude = lat;
                profile.Longitude = lng;
                await _dbContext.SaveChangesAsync();
                return Json(new { success = true });
            }

            return Json(new { success = false });
        }

        [HttpPost]
        public async Task<IActionResult> AcceptJob(int jobId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated." });

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return Json(new { success = false, message = "Job not found." });

            if (job.MechanicId != null && job.MechanicId != user.Id)
            {
                return Json(new { success = false, message = "Job has already been accepted by another mechanic." });
            }

            job.MechanicId = user.Id;
            job.Status = "Accepted";
            
            job.LastMovementTime = DateTime.UtcNow;
            job.LastLocationUpdateTime = DateTime.UtcNow;
            job.IsSimulationPaused = false;

            // Ensure mechanic profile has realistic coordinates near customer if uninitialized
            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile != null && (profile.Latitude == 0.0 || profile.Longitude == 0.0))
            {
                profile.Latitude = job.CustomerLat + 0.015;
                profile.Longitude = job.CustomerLng + 0.015;
            }

            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, jobId = job.Id });
        }

        [HttpGet]
        public async Task<IActionResult> CheckIncomingDispatch(int? currentAlertJobId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null || !profile.IsOnline || profile.KycStatus != "Approved")
            {
                return Json(new { success = true, hasJob = false });
            }

            // Check if mechanic already has an assigned active job
            var activeJob = await _dbContext.Jobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.MechanicId == user.Id && j.Status != "Completed" && j.Status != "Cancelled");

            if (activeJob != null)
            {
                return Json(new { success = true, hasActiveJob = true, activeJobId = activeJob.Id });
            }

            // Check if the current alert job was accepted by another mechanic
            if (currentAlertJobId.HasValue)
            {
                var trackedJob = await _dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == currentAlertJobId.Value);
                if (trackedJob == null || (trackedJob.MechanicId != null && trackedJob.MechanicId != user.Id) || trackedJob.Status != "Requested")
                {
                    return Json(new { success = true, hasJob = false, wasTaken = true, takenMessage = "This job request has been accepted by another mechanic." });
                }
            }

            // Search for unassigned "Requested" jobs (take latest 10 and verify age in C# to eliminate timezone discrepancies)
            var requestedJobs = await _dbContext.Jobs.AsNoTracking()
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .Where(j => j.Status == "Requested" && j.MechanicId == null)
                .OrderByDescending(j => j.CreatedAt)
                .Take(10)
                .ToListAsync();

            string userStrId = user.Id.ToString();

            foreach (var job in requestedJobs)
            {
                double jobAgeSeconds = (DateTime.UtcNow - job.CreatedAt).TotalSeconds;
                if (jobAgeSeconds >= 300) continue;

                // Dynamic radius expansion based on job age (0-20s: 15km, 20-30s: 30km, 30s+: 50km)
                double maxRadiusKm = jobAgeSeconds < 20 ? 15.0 : (jobAgeSeconds < 30 ? 30.0 : 50.0);

                // Skip if mechanic previously declined or snoozed this job
                if (!string.IsNullOrEmpty(job.DeclinedMechanicIds))
                {
                    var entries = job.DeclinedMechanicIds.Split(',').Select(id => id.Trim()).ToList();
                    bool shouldSkip = false;
                    foreach (var entry in entries)
                    {
                        if (entry == userStrId)
                        {
                            shouldSkip = true; // Permanently declined
                            break;
                        }
                        if (entry.StartsWith(userStrId + "_snooze_"))
                        {
                            string tsStr = entry.Substring((userStrId + "_snooze_").Length);
                            if (DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime snoozeUntil))
                            {
                                if (DateTime.UtcNow < snoozeUntil)
                                {
                                    shouldSkip = true; // Still snoozed/closed
                                    break;
                                }
                            }
                        }
                        if (entry.StartsWith(userStrId + "_decline_"))
                        {
                            string tsStr = entry.Substring((userStrId + "_decline_").Length);
                            if (DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime declineUntil))
                            {
                                if (DateTime.UtcNow < declineUntil)
                                {
                                    shouldSkip = true; // Still within 2 minutes of decline, skip!
                                    break;
                                }
                            }
                        }
                        if (entry.StartsWith(userStrId + "_timeout_"))
                        {
                            string countStr = entry.Substring((userStrId + "_timeout_").Length);
                            if (int.TryParse(countStr, out int timeoutCount) && timeoutCount >= 5)
                            {
                                shouldSkip = true; // Permanently skip after 5 timeouts
                                break;
                            }
                        }
                        if (entry.StartsWith(userStrId + "_cooldown_"))
                        {
                            string tsStr = entry.Substring((userStrId + "_cooldown_").Length);
                            if (DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime cooldownUntil))
                            {
                                if (DateTime.UtcNow < cooldownUntil)
                                {
                                    shouldSkip = true; // Still cooling down
                                    break;
                                }
                            }
                        }
                    }
                    if (shouldSkip) continue;
                }

                // Check distance within expanding radius limit (bypass if mechanic location is uninitialized 0, 0)
                double distanceKm = _dispatchEngine.CalculateDistance(job.CustomerLat, job.CustomerLng, profile.Latitude, profile.Longitude);
                bool isLocNotSet = (profile.Latitude == 0.0 && profile.Longitude == 0.0);
                if (isLocNotSet || distanceKm <= maxRadiusKm)
                {
                    string rawPhone = job.Customer?.PhoneNumber ?? "9876543210";
                    string maskedPhone = rawPhone.Length >= 4 ? "+91 XXXXX " + rawPhone.Substring(rawPhone.Length - 4) : "+91 XXXXX XXXX";
                    string approxLoc = !string.IsNullOrEmpty(job.Landmark) ? job.Landmark : (job.Address.Contains(",") ? job.Address.Split(',')[0] : "Sector 62 Noida");
                    int etaMins = (int)Math.Round(distanceKm * 3.2 + 4);

                    return Json(new
                    {
                        success = true,
                        hasJob = true,
                        jobId = job.Id,
                        customerName = job.Customer?.Name ?? "Rahul Sharma",
                        customerPhoneMasked = maskedPhone,
                        vehicleType = job.Vehicle?.VehicleType ?? "Car",
                        vehicleModel = job.Vehicle?.Model ?? "Hyundai i20",
                        vehicleReg = job.Vehicle?.RegistrationNumber ?? "UP32 AB 1234",
                        problemType = job.ProblemType,
                        problemDescription = string.IsNullOrEmpty(job.ProblemDescription) ? "Emergency roadside assistance required." : job.ProblemDescription,
                        approxLocation = approxLoc,
                        distanceKm = Math.Round(distanceKm, 1),
                        etaMinutes = etaMins,
                        estEarningsMin = (int)Math.Round(_paymentService.CalculateTieredCommissionAndNetEarnings(job.VisitingCharge + job.ServiceChargeMin, 0).MechanicNetEarningAmount),
                        estEarningsMax = (int)Math.Round(_paymentService.CalculateTieredCommissionAndNetEarnings(job.VisitingCharge + job.ServiceChargeMax, 0).MechanicNetEarningAmount),
                        smartScore = 5,
                        smartMatchTag = $"{job.ProblemType} Expert Match",
                        acceptanceChance = "High"
                    });
                }
            }

            return Json(new { success = true, hasJob = false });
        }

        [HttpPost]
        public async Task<IActionResult> DeclineJob(int jobId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job != null)
            {
                string userStrId = user.Id.ToString();
                string declineTimestamp = DateTime.UtcNow.AddMinutes(2).ToString("yyyy-MM-ddTHH:mm:ssZ");
                string declineEntry = $"{userStrId}_decline_{declineTimestamp}";

                if (string.IsNullOrEmpty(job.DeclinedMechanicIds))
                {
                    job.DeclinedMechanicIds = declineEntry;
                }
                else
                {
                    var ids = job.DeclinedMechanicIds.Split(',').Select(i => i.Trim()).ToList();
                    // Clean up any old decline/snooze entries for this user
                    ids.RemoveAll(id => id == userStrId || id.StartsWith(userStrId + "_snooze_") || id.StartsWith(userStrId + "_decline_"));
                    ids.Add(declineEntry);
                    job.DeclinedMechanicIds = string.Join(",", ids);
                }
                await _dbContext.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SnoozeJob(int jobId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job != null)
            {
                string userStrId = user.Id.ToString();
                string snoozeTimestamp = DateTime.UtcNow.AddMinutes(10).ToString("yyyy-MM-ddTHH:mm:ssZ");
                string snoozeEntry = $"{userStrId}_snooze_{snoozeTimestamp}";

                if (string.IsNullOrEmpty(job.DeclinedMechanicIds))
                {
                    job.DeclinedMechanicIds = snoozeEntry;
                }
                else
                {
                    var ids = job.DeclinedMechanicIds.Split(',').Select(i => i.Trim()).ToList();
                    // Clean up any old entries for this user
                    ids.RemoveAll(id => id == userStrId || id.StartsWith(userStrId + "_snooze_"));
                    ids.Add(snoozeEntry);
                    job.DeclinedMechanicIds = string.Join(",", ids);
                }
                await _dbContext.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> TimeoutJob(int jobId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job != null)
            {
                string userStrId = user.Id.ToString();
                
                int currentTimeoutCount = 0;
                var ids = new List<string>();
                if (!string.IsNullOrEmpty(job.DeclinedMechanicIds))
                {
                    ids = job.DeclinedMechanicIds.Split(',').Select(i => i.Trim()).ToList();
                    
                    var timeoutEntry = ids.FirstOrDefault(id => id.StartsWith(userStrId + "_timeout_"));
                    if (timeoutEntry != null)
                    {
                        string countStr = timeoutEntry.Substring((userStrId + "_timeout_").Length);
                        int.TryParse(countStr, out currentTimeoutCount);
                        ids.Remove(timeoutEntry);
                    }
                    
                    ids.RemoveAll(id => id.StartsWith(userStrId + "_cooldown_"));
                }
                
                currentTimeoutCount++;
                ids.Add($"{userStrId}_timeout_{currentTimeoutCount}");
                
                if (currentTimeoutCount < 5)
                {
                    string cooldownTimestamp = DateTime.UtcNow.AddSeconds(3).ToString("yyyy-MM-ddTHH:mm:ssZ");
                    ids.Add($"{userStrId}_cooldown_{cooldownTimestamp}");
                }
                else
                {
                    ids.Add(userStrId);
                }
                
                job.DeclinedMechanicIds = string.Join(",", ids);
                await _dbContext.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> DebugDecline(int jobId, int userId)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return Content("Job not found");
            
            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null) return Content("Profile not found");

            string userStrId = userId.ToString();
            var logs = new List<string>();
            logs.Add($"Job ID: {job.Id}, Status: {job.Status}, DeclinedMechanicIds: {job.DeclinedMechanicIds}");
            logs.Add($"UtcNow: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
            
            if (!string.IsNullOrEmpty(job.DeclinedMechanicIds))
            {
                var entries = job.DeclinedMechanicIds.Split(',').Select(id => id.Trim()).ToList();
                foreach (var entry in entries)
                {
                    if (entry == userStrId)
                    {
                        logs.Add($"Entry '{entry}' == userStrId '{userStrId}': shouldSkip = true");
                    }
                    if (entry.StartsWith(userStrId + "_decline_"))
                    {
                        string tsStr = entry.Substring((userStrId + "_decline_").Length);
                        var parseSuccess = DateTime.TryParse(tsStr, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTime declineUntil);
                        logs.Add($"Entry '{entry}' matches decline. ParseSuccess: {parseSuccess}, Parsed declineUntil: {declineUntil:yyyy-MM-ddTHH:mm:ssZ}, Kind: {declineUntil.Kind}");
                        var isStillBlocked = DateTime.UtcNow < declineUntil;
                        logs.Add($"UtcNow < declineUntil: {isStillBlocked} (shouldSkip = {isStillBlocked})");
                    }
                }
            }

            return Content(string.Join("\n", logs));
        }

        [HttpPost]
        public async Task<IActionResult> CollectPaymentWithQr(int jobId, string paymentMode = "UPI_QR")
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated." });

            var job = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null) return Json(new { success = false, message = "Job not found." });
            if (job.MechanicId != user.Id) return Json(new { success = false, message = "Unauthorized access to job." });

            var mechProfile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (mechProfile == null) return Json(new { success = false, message = "Mechanic profile not found." });

            string payId = "pay_qr_" + Guid.NewGuid().ToString().Substring(0, 12);

            bool success = await _paymentService.ProcessEscrowPaymentForJobAsync(job.Id, payId);
            if (!success) return Json(new { success = false, message = "Failed to process QR payment." });

            // Fetch updated invoice breakdown via IPaymentService
            var breakdown = await _paymentService.GenerateJobInvoiceBreakdownAsync(job.Id);

            return Json(new
            {
                success = true,
                message = $"Payment of ₹{breakdown.TotalBillAmount:N2} collected successfully!",
                jobId = job.Id,
                finalBillAmount = breakdown.TotalBillAmount,
                adminCommission = breakdown.AdminCommission,
                mechanicNetEarning = breakdown.MechanicNetEarning,
                commissionPercent = breakdown.CommissionPercent,
                newWalletBalance = mechProfile.CurrentEarnings
            });
        }

        [HttpPost]
        public async Task<IActionResult> SettleCashPayment(int jobId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated." });

            var job = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .FirstOrDefaultAsync(j => j.Id == jobId && j.MechanicId == user.Id);

            if (job == null) return Json(new { success = false, message = "Job not found." });
            if (job.Status == "Completed" || job.Status == "Cancelled")
                return Json(new { success = false, message = "Job is already completed or cancelled." });

            string cashPaymentId = "pay_cash_" + Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            bool success = await _paymentService.ProcessEscrowPaymentForJobAsync(jobId, cashPaymentId);

            if (success)
            {
                var mechProfile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                double balance = mechProfile?.CurrentEarnings ?? 0.0;
                
                return Json(new { 
                    success = true, 
                    message = "Job successfully settled in Cash! Platform commission fee has been adjusted in your wallet.",
                    newWalletBalance = balance
                });
            }

            return Json(new { success = false, message = "Failed to process cash settlement." });
        }

        [HttpGet]
        public async Task<IActionResult> GetJobInvoiceDetails(int jobId)
        {
            var job = await _dbContext.Jobs
                .AsNoTracking()
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null) return Json(new { success = false, message = "Job not found." });

            var mechProfile = await _dbContext.MechanicProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == job.MechanicId);
            var payment = await _dbContext.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.JobId == jobId);

            double baseEstBill = job.VisitingCharge + job.ServiceChargeMin;
            double totalBill = job.FinalBillAmount > baseEstBill ? job.FinalBillAmount : baseEstBill;
            double partsAmt = (job.PartsApproved == true) ? job.PartsEstimateAmount : 0;
            var commCalc = _paymentService.CalculateTieredCommissionAndNetEarnings(totalBill, partsAmt);

            double adminCommission = payment != null && payment.AdminCommissionAmount > 0 ? payment.AdminCommissionAmount : commCalc.AdminCommissionAmount;
            double mechanicNetEarning = payment != null && payment.MechanicEarningAmount > 0 ? payment.MechanicEarningAmount : commCalc.MechanicNetEarningAmount;
            double effectiveCommRatePct = (payment?.CommissionRateUsed ?? commCalc.CommissionRate) * 100;

            return Json(new
            {
                success = true,
                invoiceNo = $"RS-INV-{job.Id:D4}-{DateTime.Now.Year}",
                jobId = job.Id,
                date = (job.CompletedAt ?? job.CreatedAt).ToString("dd MMM yyyy, hh:mm tt"),
                status = job.Status,
                customerName = job.Customer?.Name ?? "Customer",
                customerPhone = job.Customer?.PhoneNumber ?? "N/A",
                customerAddress = job.Address,
                vehicleModel = job.Vehicle?.Model ?? "Vehicle",
                vehicleType = job.Vehicle?.VehicleType ?? "Car",
                vehicleRegNumber = job.Vehicle?.RegistrationNumber ?? "UP32 AB 1234",
                fuelType = job.FuelType,
                mechanicName = job.Mechanic?.Name ?? "Verified Technician",
                mechanicPhone = job.Mechanic?.PhoneNumber ?? "N/A",
                shopName = mechProfile?.ShopName ?? "RaahSathi Partner Garage",
                shopAddress = mechProfile?.ShopAddress ?? "Sector 62 Noida",
                problemType = job.ProblemType,
                
                // Itemized Breakdown
                visitingCharge = job.VisitingCharge,
                serviceChargeMin = job.ServiceChargeMin,
                customEstimateAmount = job.CustomEstimateApproved == true ? job.CustomEstimateAmount : 0.0,
                customEstimateDetails = job.CustomEstimateDetails,
                partsEstimateAmount = job.PartsApproved == true ? job.PartsEstimateAmount : 0.0,
                partsMrp = job.PartsApproved == true ? (job.PartsMrp > 0 ? job.PartsMrp : job.PartsEstimateAmount) : 0.0,
                extraLabourCharge = job.PartsApproved == true ? job.ExtraLabourCharge : 0.0,
                partsDetails = job.ExtraPartsName,
                towingCharge = job.TowingApproved == true ? job.TowingCharge : 0.0,
                
                // Totals & Commission
                totalBillAmount = totalBill,
                adminCommission = adminCommission,
                mechanicNetEarning = mechanicNetEarning,
                commissionPercent = effectiveCommRatePct,
                adminUpiId = (await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminUpiId"))?.SettingValue ?? "raahsathi@upi",
                adminAccountHolderName = (await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminAccountHolderName"))?.SettingValue ?? "RaahSathi URA"
            });
        }

        [HttpPost]
        public async Task<IActionResult> AdvanceJobStatus(int jobId, string newStatus)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated." });

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return Json(new { success = false, message = "Job not found." });

            if (newStatus.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                // Strict Validation: Verify if payment has been collected and processed in Payments table
                var payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.JobId == jobId && (p.PaymentStatus == "Released" || p.PaymentStatus == "Paid" || p.PaymentStatus == "Completed" || p.PaymentStatus == "Captured"));

                if (payment == null)
                {
                    return Json(new { 
                        success = false, 
                        requiresPayment = true,
                        message = "⚠️ Payment Pending! Job cannot be marked as Completed until customer payment is collected. Please click 'Collect Payment' to generate QR Code or confirm payment." 
                    });
                }

                job.CompletedAt = DateTime.UtcNow;
            }

            job.Status = newStatus;

            if (newStatus.Equals("Driving", StringComparison.OrdinalIgnoreCase))
            {
                job.LastMovementTime = DateTime.UtcNow;
                job.LastLocationUpdateTime = DateTime.UtcNow;
                job.IsSimulationPaused = false;
            }

            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitCustomEstimate(int jobId, double amount, string details)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return NotFound();

            job.CustomEstimateAmount = amount;
            job.CustomEstimateDetails = details;
            job.CustomEstimateApproved = null; // Reset to pending customer approval
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSparesQuote(int jobId, string partName, double partPrice, double labourPrice, double partsMrp = 0)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return NotFound();

            if (partsMrp <= 0) partsMrp = partPrice;

            job.ExtraPartsName = partName;
            job.PartsMrp = partsMrp;
            job.PartsEstimateAmount = partPrice;
            job.ExtraLabourCharge = labourPrice;
            job.PartsEstimateDetails = partsMrp > partPrice 
                ? $"{partName} (MRP: ₹{partsMrp:N0}, Billed: ₹{partPrice:N0}) + Labour (₹{labourPrice:N0})"
                : $"{partName} (₹{partPrice:N0}) + Labour (₹{labourPrice:N0})";
            job.PartsApproved = null; // Reset to pending customer approval
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveJobState(int jobId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var job = await _dbContext.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == jobId && j.MechanicId == user.Id);
            if (job == null) return Json(new { success = false, message = "Job not found" });

            double inactiveSeconds = 0;
            if ((job.Status == "Accepted" || job.Status == "Driving") && job.LastMovementTime.HasValue)
            {
                inactiveSeconds = (DateTime.UtcNow - job.LastMovementTime.Value).TotalSeconds;
            }

            int unreadChatCount = await _dbContext.JobChatMessages
                .AsNoTracking()
                .CountAsync(m => m.JobId == jobId && m.SenderRole == "Customer" && !m.IsRead);

            return Json(new
            {
                success = true,
                jobId = job.Id,
                status = job.Status,
                customEstimateAmount = job.CustomEstimateAmount,
                customEstimateDetails = job.CustomEstimateDetails,
                customEstimateApproved = job.CustomEstimateApproved,
                partsEstimateAmount = job.PartsEstimateAmount,
                partsApproved = job.PartsApproved,
                towingApproved = job.TowingApproved,
                finalBillAmount = job.FinalBillAmount,
                problemType = job.ProblemType,
                selectedProblemsJson = job.SelectedProblemsJson,
                cancelledProblemItem = job.CancelledProblemItem,
                problemCancelReason = job.ProblemCancelReason,
                problemCancelDescription = job.ProblemCancelDescription,
                problemCancelledAt = job.ProblemCancelledAt?.ToString("o"),
                isSimulationPaused = job.IsSimulationPaused,
                inactiveSeconds = inactiveSeconds,
                unreadChatCount = unreadChatCount
            });
        }

        [HttpPost]
        public async Task<IActionResult> CancelJobProblemItem(int jobId, string problemName, string reason, string? description)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated." });

            var job = await _dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId && j.MechanicId == user.Id);
            if (job == null) return Json(new { success = false, message = "Job not found or not assigned to you." });

            // 1. Validation: Allowed only in Inspecting (or Arrived) and Repairing stages
            if (!string.Equals(job.Status, "Inspecting", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(job.Status, "Arrived", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(job.Status, "Repairing", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, message = "Problems can only be dropped/cancelled during Inspection or Repairing stages." });
            }

            // 2. Validation: Only 1 problem can be cancelled across the whole job
            if (!string.IsNullOrEmpty(job.CancelledProblemItem))
            {
                return Json(new { success = false, message = $"Only 1 problem item can be cancelled per job. Problem '{job.CancelledProblemItem}' was already dropped." });
            }

            // 3. Validation: Must have at least 2 problems in total
            var problems = (job.ProblemType ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (problems.Count < 2)
            {
                return Json(new { success = false, message = "A problem item can only be cancelled if the job contains 2 or more problems." });
            }

            // 4. Validation: Check that the requested problemName is in the job
            var matchedProblem = problems.FirstOrDefault(p => p.Equals(problemName, StringComparison.OrdinalIgnoreCase) || p.Contains(problemName, StringComparison.OrdinalIgnoreCase) || problemName.Contains(p, StringComparison.OrdinalIgnoreCase));
            if (matchedProblem == null)
            {
                return Json(new { success = false, message = "Specified problem was not found in this job request." });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return Json(new { success = false, message = "Please select a valid cancellation reason." });
            }

            // Calculate price to deduct
            var (minRate, _) = _pricingEngine.GetServiceChargeRange(matchedProblem);
            double deductionAmount = minRate > 0 ? minRate : 150;

            // Mark job fields
            job.CancelledProblemItem = matchedProblem;
            job.ProblemCancelReason = reason;
            job.ProblemCancelDescription = description ?? "";
            job.ProblemCancelledAt = DateTime.UtcNow;

            // Recalculate bill
            job.ServiceChargeMin = Math.Max(0, job.ServiceChargeMin - deductionAmount);
            job.FinalBillAmount = Math.Max(job.VisitingCharge, job.FinalBillAmount - deductionAmount);

            // Audit log
            var audit = new AuditLog
            {
                ActionType = "CANCEL_JOB_PROBLEM",
                AdminName = user.Name,
                UserRole = "Mechanic",
                TimeStamp = DateTime.UtcNow,
                Details = $"Job #{job.Id}: Mechanic {user.Name} dropped problem '{matchedProblem}' (Deducted: ₹{deductionAmount}). Reason: {reason}. Notes: {description}"
            };
            _dbContext.AuditLogs.Add(audit);

            await _dbContext.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Problem '{matchedProblem}' successfully dropped. Bill deducted by -₹{deductionAmount}.",
                cancelledProblem = matchedProblem,
                reason = reason,
                description = description,
                newServiceChargeMin = job.ServiceChargeMin,
                newFinalBillAmount = job.FinalBillAmount
            });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleSimulationPause(int jobId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null || job.MechanicId != user.Id) return Json(new { success = false, message = "Job not found" });

            job.IsSimulationPaused = !job.IsSimulationPaused;
            if (!job.IsSimulationPaused)
            {
                job.LastMovementTime = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, isSimulationPaused = job.IsSimulationPaused });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitPartsEstimate(int jobId, double amount, string details)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return NotFound();

            job.PartsEstimateAmount = amount;
            job.PartsEstimateDetails = details;
            job.PartsApproved = null;
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RequestTowing(int jobId, double amount, string reason)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return NotFound();

            job.TowingNeeded = true;
            job.TowingCharge = amount;
            job.TowingReason = reason;
            job.TowingProofPhoto = "/images/mock_tow.jpg"; // Mock path
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfileSettings(
            string? name, string? phoneNumber, string? email, string? shopName, string? shopAddress, int? experienceYears,
            IFormFile? profilePhoto, string[]? vehicleExpertise, string[]? specialization, int serviceRadiusKm = 10, 
            string? languages = null, string? workingHours = null, string? bankName = null, string? accountNumber = null, 
            string? ifscCode = null, string? upiId = null, string? accountHolderName = null, string? city = null, 
            string? preferredPayoutMethod = null, bool acceptsCash = true)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null) return Json(new { success = false, message = "Profile not found." });

            var changedFields = new List<string>();

            // 1. Name
            if (!string.IsNullOrWhiteSpace(name) && name.Trim() != user.Name)
            {
                changedFields.Add($"Name ('{user.Name}' ➔ '{name.Trim()}')");
                user.Name = name.Trim();
            }

            // 2. Phone Number
            if (!string.IsNullOrWhiteSpace(phoneNumber) && phoneNumber.Trim() != user.PhoneNumber)
            {
                changedFields.Add($"Phone ('{user.PhoneNumber}' ➔ '{phoneNumber.Trim()}')");
                user.PhoneNumber = phoneNumber.Trim();
            }

            // 3. Email
            if (!string.IsNullOrWhiteSpace(email) && email.Trim() != profile.Email)
            {
                changedFields.Add($"Email ('{profile.Email}' ➔ '{email.Trim()}')");
                profile.Email = email.Trim();
            }

            // 4. Shop / Workshop Name
            if (!string.IsNullOrWhiteSpace(shopName) && shopName.Trim() != profile.ShopName)
            {
                changedFields.Add($"Workshop Name ('{profile.ShopName}' ➔ '{shopName.Trim()}')");
                profile.ShopName = shopName.Trim();
                profile.GarageName = shopName.Trim();
            }

            // 5. Shop / Workshop Address
            if (!string.IsNullOrWhiteSpace(shopAddress) && shopAddress.Trim() != profile.ShopAddress)
            {
                changedFields.Add($"Address ('{profile.ShopAddress}' ➔ '{shopAddress.Trim()}')");
                profile.ShopAddress = shopAddress.Trim();
            }

            // 6. Experience in Years
            if (experienceYears.HasValue && experienceYears.Value != profile.ExperienceYears)
            {
                changedFields.Add($"Experience ({profile.ExperienceYears} yrs ➔ {experienceYears.Value} yrs)");
                profile.ExperienceYears = experienceYears.Value;
            }

            // 7. Profile Photo
            if (profilePhoto != null && profilePhoto.Length > 0)
            {
                string ext = Path.GetExtension(profilePhoto.FileName).ToLowerInvariant();
                var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                if (allowedExts.Contains(ext))
                {
                    var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                    Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = $"mech_avatar_{user.Id}_{Guid.NewGuid():N}{ext}";
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await profilePhoto.CopyToAsync(stream);
                    }
                    profile.ProfilePhotoUrl = "/uploads/" + uniqueFileName;
                    changedFields.Add("Profile Photo");
                }
            }

            // 8. Expertise & Skills
            if (vehicleExpertise != null && vehicleExpertise.Length > 0)
            {
                var newExp = string.Join(", ", vehicleExpertise);
                if (newExp != profile.VehicleExpertise)
                {
                    changedFields.Add("Vehicle Expertise");
                    profile.VehicleExpertise = newExp;
                }
            }

            if (specialization != null && specialization.Length > 0)
            {
                var newSpec = string.Join(", ", specialization);
                if (newSpec != profile.Specialization)
                {
                    changedFields.Add("Specialization Skills");
                    profile.Specialization = newSpec;
                }
            }

            // 9. Service Radius
            if (serviceRadiusKm > 0 && serviceRadiusKm != profile.ServiceRadiusKm)
            {
                changedFields.Add($"Service Radius ({profile.ServiceRadiusKm}KM ➔ {serviceRadiusKm}KM)");
                profile.ServiceRadiusKm = serviceRadiusKm;
            }

            // 10. City & Operating details
            if (!string.IsNullOrWhiteSpace(city) && city.Trim() != profile.City)
            {
                changedFields.Add($"Operating City ('{profile.City}' ➔ '{city.Trim()}')");
                profile.City = city.Trim();
            }

            if (!string.IsNullOrWhiteSpace(languages) && languages != profile.Languages)
            {
                profile.Languages = languages;
            }

            if (!string.IsNullOrWhiteSpace(workingHours) && workingHours != profile.WorkingHours)
            {
                profile.WorkingHours = workingHours;
            }

            // 11. Payment details
            if (bankName != null) profile.BankName = bankName;
            if (accountNumber != null) profile.BankAccountNumber = accountNumber;
            if (ifscCode != null) profile.IfscCode = ifscCode;
            if (upiId != null) profile.UpiId = upiId;
            if (accountHolderName != null) profile.AccountHolderName = accountHolderName;
            if (!string.IsNullOrWhiteSpace(preferredPayoutMethod)) profile.PreferredPayoutMethod = preferredPayoutMethod;
            profile.AcceptsCash = acceptsCash;

            await _dbContext.SaveChangesAsync();

            // 12. Create Audit Log for Admin Notification if any changes were made
            if (changedFields.Count > 0)
            {
                var auditLog = new AuditLog
                {
                    AdminName = user.Name,
                    UserRole = "Mechanic",
                    ActionType = "MECHANIC_PROFILE_UPDATE",
                    Details = $"Mechanic {user.Name} (ID: RS{user.Id:D2}M, Phone: {user.PhoneNumber}) updated: {string.Join(", ", changedFields)}",
                    TimeStamp = DateTime.UtcNow,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1",
                    UserAgent = Request.Headers["User-Agent"].ToString() ?? "Mobile/Web App"
                };
                _dbContext.AuditLogs.Add(auditLog);
                await _dbContext.SaveChangesAsync();
            }

            return Json(new { 
                success = true, 
                message = "Profile details updated successfully!", 
                profilePhotoUrl = profile.ProfilePhotoUrl,
                name = user.Name,
                phone = user.PhoneNumber
            });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBankDetails(
            string preferredPayoutMethod, string? upiId, string? accountHolderName,
            string? bankName, string? accountNumber, string? ifscCode)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null) return Json(new { success = false, message = "Profile not found." });

            preferredPayoutMethod = (preferredPayoutMethod ?? "UPI").Trim();

            // 1. Validation based on Payment Mode
            if (preferredPayoutMethod.Equals("UPI", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(upiId) || !upiId.Contains('@'))
                {
                    return Json(new { success = false, message = "⚠️ Please enter a valid UPI ID (e.g. 9876543210@paytm or yourname@okhdfcbank)." });
                }
                upiId = upiId.Trim();
            }
            else if (preferredPayoutMethod.Equals("Bank", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(accountHolderName))
                {
                    return Json(new { success = false, message = "⚠️ Please enter the Account Holder Name as per bank passbook." });
                }
                if (string.IsNullOrWhiteSpace(bankName))
                {
                    return Json(new { success = false, message = "⚠️ Please enter your Bank Name (e.g. SBI, HDFC, ICICI)." });
                }
                if (string.IsNullOrWhiteSpace(accountNumber))
                {
                    return Json(new { success = false, message = "⚠️ Please enter your Bank Account Number." });
                }
                if (string.IsNullOrWhiteSpace(ifscCode) || ifscCode.Trim().Length < 5)
                {
                    return Json(new { success = false, message = "⚠️ Please enter a valid Bank IFSC Code (e.g. SBIN0001234)." });
                }

                accountHolderName = accountHolderName.Trim();
                bankName = bankName.Trim();
                accountNumber = accountNumber.Trim();
                ifscCode = ifscCode.Trim().ToUpper();
            }
            else
            {
                preferredPayoutMethod = "UPI";
            }

            // 2. Execute Stored Procedure: rs_mechanicprofiles_update_bank_details
            bool spExecuted = false;
            try
            {
                if (_dbContext.Database.IsSqlServer())
                {
                    await _dbContext.Database.ExecuteSqlRawAsync(
                        "EXEC dbo.rs_mechanicprofiles_update_bank_details @MechanicUserId = {0}, @PreferredPayoutMethod = {1}, @UpiId = {2}, @AccountHolderName = {3}, @BankName = {4}, @BankAccountNumber = {5}, @IfscCode = {6}",
                        user.Id, preferredPayoutMethod, (object?)upiId ?? DBNull.Value, (object?)accountHolderName ?? DBNull.Value, (object?)bankName ?? DBNull.Value, (object?)accountNumber ?? DBNull.Value, (object?)ifscCode ?? DBNull.Value
                    );
                    spExecuted = true;
                }
            }
            catch
            {
                // Fallback handled below
            }

            // Sync EF Core entity model
            profile.PreferredPayoutMethod = preferredPayoutMethod;
            if (upiId != null) profile.UpiId = upiId;
            if (accountHolderName != null) profile.AccountHolderName = accountHolderName;
            if (bankName != null) profile.BankName = bankName;
            if (accountNumber != null) profile.BankAccountNumber = accountNumber;
            if (ifscCode != null) profile.IfscCode = ifscCode;

            if (!spExecuted)
            {
                await _dbContext.SaveChangesAsync();
            }

            return Json(new
            {
                success = true,
                message = "Bank & payout details updated successfully via Stored Procedure!",
                preferredPayoutMethod = profile.PreferredPayoutMethod,
                upiId = profile.UpiId ?? "",
                accountHolderName = profile.AccountHolderName ?? "",
                bankName = profile.BankName ?? "",
                accountNumber = profile.BankAccountNumber ?? "",
                ifscCode = profile.IfscCode ?? ""
            });
        }

        [HttpPost]
        public async Task<IActionResult> WithdrawWallet(
            double amount, string payoutMethod, string accountHolderName, 
            string accountNumber, string bankName, string ifscCode, string upiId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            using (var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                    if (profile == null) return Json(new { success = false, message = "Profile not found." });

                    if (amount <= 0 || amount > profile.CurrentEarnings)
                    {
                        return Json(new { success = false, message = "Invalid withdrawal amount." });
                    }

                    payoutMethod = payoutMethod ?? "Bank";

                    // Validate mandatory fields
                    if (payoutMethod == "Bank")
                    {
                        if (string.IsNullOrEmpty(accountHolderName) || string.IsNullOrEmpty(accountNumber) ||
                            string.IsNullOrEmpty(bankName) || string.IsNullOrEmpty(ifscCode))
                        {
                            return Json(new { success = false, message = "All bank account details are mandatory for Bank Payout." });
                        }
                    }
                    else if (payoutMethod == "UPI")
                    {
                        if (string.IsNullOrEmpty(upiId))
                        {
                            return Json(new { success = false, message = "UPI ID is mandatory for UPI Payout." });
                        }
                    }

                    // Deduct the earnings (holding them in the pending request)
                    profile.CurrentEarnings -= amount;

                    // Also update the profile settings in the database for future convenience
                    profile.PreferredPayoutMethod = payoutMethod;
                    if (payoutMethod == "Bank")
                    {
                        profile.AccountHolderName = accountHolderName.Trim();
                        profile.BankAccountNumber = accountNumber.Trim();
                        profile.BankName = bankName.Trim();
                        profile.IfscCode = ifscCode.Trim();
                    }
                    else
                    {
                        profile.UpiId = upiId.Trim();
                    }

                    // Create a payout request
                    var payoutRequest = new MechanicPayoutRequest
                    {
                        MechanicId = user.Id,
                        Amount = amount,
                        PayoutMethod = payoutMethod,
                        AccountHolderName = payoutMethod == "Bank" ? accountHolderName.Trim() : string.Empty,
                        BankAccountNumber = payoutMethod == "Bank" ? accountNumber.Trim() : string.Empty,
                        BankName = payoutMethod == "Bank" ? bankName.Trim() : string.Empty,
                        IfscCode = payoutMethod == "Bank" ? ifscCode.Trim() : string.Empty,
                        UpiId = payoutMethod == "UPI" ? upiId.Trim() : string.Empty,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow
                      };

                      _dbContext.MechanicPayoutRequests.Add(payoutRequest);
                      await _dbContext.SaveChangesAsync();

                      await transaction.CommitAsync();

                      return Json(new { success = true, remainingBalance = profile.CurrentEarnings, message = "Withdrawal request submitted successfully! Pending Admin verification and payout release." });
                  }
                  catch (Exception)
                  {
                      await transaction.RollbackAsync();
                      return Json(new { success = false, message = "An error occurred during withdrawal processing. Please try again." });
                  }
              }
          }

        [HttpPost]
        public async Task<IActionResult> InstantPayout()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return RedirectToAction("Login", "Auth");

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile != null && profile.CurrentEarnings > 0)
            {
                double amount = profile.CurrentEarnings;
                profile.CurrentEarnings = 0; // Payout wallet reset

                // Create a completed payout request for auditing
                var payoutRequest = new MechanicPayoutRequest
                {
                    MechanicId = user.Id,
                    Amount = amount,
                    PayoutMethod = profile.PreferredPayoutMethod ?? "UPI",
                    AccountHolderName = profile.AccountHolderName ?? string.Empty,
                    BankAccountNumber = profile.BankAccountNumber ?? string.Empty,
                    BankName = profile.BankName ?? string.Empty,
                    IfscCode = profile.IfscCode ?? string.Empty,
                    UpiId = profile.UpiId ?? string.Empty,
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow,
                    AdminRemarks = "Automated Instant Payout",
                    TransactionReference = "TXN_" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper()
                };

                _dbContext.MechanicPayoutRequests.Add(payoutRequest);
                await _dbContext.SaveChangesAsync();
                TempData["Success"] = $"Payout of ₹{amount:F2} successfully deposited to bank account linked via UPI.";
            }

            return RedirectToAction("Dashboard");
        }

        [HttpGet]
        public async Task<IActionResult> GetSupportMessages()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var list = await _dbContext.MechanicSupportMessages
                .Where(m => m.MechanicId == user.Id)
                .OrderBy(m => m.SentAt)
                .Select(m => new {
                    id = m.Id,
                    title = m.Title,
                    messageText = m.MessageText,
                    senderRole = m.SenderRole,
                    senderName = m.SenderName,
                    isFromAdmin = m.IsFromAdmin,
                    sentTime = m.SentAt.ToLocalTime().ToString("dd MMM, hh:mm tt")
                })
                .ToListAsync();

            // Mark unread admin messages as read
            var unread = await _dbContext.MechanicSupportMessages.Where(m => m.MechanicId == user.Id && !m.IsRead && m.IsFromAdmin).ToListAsync();
            if (unread.Any())
            {
                foreach (var u in unread) u.IsRead = true;
                await _dbContext.SaveChangesAsync();
            }

            return Json(new { success = true, messages = list });
        }

        [HttpPost]
        public async Task<IActionResult> SendReplyToSupport(string message, string? title)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, message = "Message text cannot be empty." });
            }

            var msg = new MechanicSupportMessage
            {
                MechanicId = user.Id,
                Title = string.IsNullOrWhiteSpace(title) ? "Re: Support Inquiry" : title.Trim(),
                MessageText = message.Trim(),
                SenderRole = "Mechanic",
                SenderName = user.Name,
                IsFromAdmin = false,
                IsRead = false,
                SentAt = DateTime.UtcNow
            };

            _dbContext.MechanicSupportMessages.Add(msg);
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Reply sent to Operations Support Team!" });
        }

        [HttpGet]
        public async Task<IActionResult> GetWalletStats(string? fromDate, string? toDate)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null) return Json(new { success = false, message = "Profile not found" });

            var allPayments = await _dbContext.Payments
                .Include(p => p.Job)
                .Where(p => p.Job != null && p.Job.MechanicId == user.Id)
                .ToListAsync();

            var releasedPayments = allPayments.Where(p => p.PaymentStatus == "Released" || p.PaymentStatus == "Completed" || p.PaymentStatus == "Paid").ToList();
            var heldPayments = allPayments.Where(p => p.PaymentStatus == "Held" || p.PaymentStatus == "Pending").ToList();

            var nowLocal = DateTime.UtcNow.ToLocalTime();
            var todayLocal = nowLocal.Date;

            // Current Week: Monday 00:00 to next Monday 00:00
            int diffToMonday = (7 + (int)todayLocal.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            var startOfWeek = todayLocal.AddDays(-diffToMonday);
            var endOfWeek = startOfWeek.AddDays(7);

            // Current Month: 1st of month 00:00 to 1st of next month 00:00
            var startOfMonth = new DateTime(todayLocal.Year, todayLocal.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1);

            double todayEarnings = releasedPayments
                .Where(p => p.CreatedAt.ToLocalTime().Date == todayLocal)
                .Sum(p => p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount));

            double weeklyEarnings = releasedPayments
                .Where(p => {
                    var d = p.CreatedAt.ToLocalTime().Date;
                    return d >= startOfWeek && d < endOfWeek;
                })
                .Sum(p => p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount));

            double monthlyEarnings = releasedPayments
                .Where(p => {
                    var d = p.CreatedAt.ToLocalTime().Date;
                    return d >= startOfMonth && d < endOfMonth;
                })
                .Sum(p => p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount));

            // Current Month Total Withdrawal (Completed / Approved Payout Requests)
            double monthlyWithdrawal = await _dbContext.MechanicPayoutRequests
                .Where(r => r.MechanicId == user.Id && (r.Status == "Approved" || r.Status == "Completed") && r.CreatedAt >= startOfMonth.ToUniversalTime() && r.CreatedAt < endOfMonth.ToUniversalTime())
                .SumAsync(r => (double?)r.Amount) ?? 0.0;

            double heldEarnings = heldPayments.Sum(p => p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount));
            double pendingSettlement = profile.CurrentEarnings;

            var todayUtc = DateTime.UtcNow.Date;
            var startOfWeekUtc = startOfWeek.ToUniversalTime();
            int todayJobsCount = await _dbContext.Jobs
                .CountAsync(j => j.MechanicId == user.Id && j.Status == "Completed" && (j.CompletedAt ?? j.CreatedAt) >= todayUtc);
            int weeklyJobsCount = await _dbContext.Jobs
                .CountAsync(j => j.MechanicId == user.Id && j.Status == "Completed" && (j.CompletedAt ?? j.CreatedAt) >= startOfWeekUtc);

            // Filter statement by Date Range (Default: Last 1 Month if not provided)
            DateTime filterFromDate = todayLocal.AddMonths(-1);
            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out DateTime parsedFrom))
            {
                filterFromDate = parsedFrom.Date;
            }

            DateTime filterToDate = todayLocal;
            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out DateTime parsedTo))
            {
                filterToDate = parsedTo.Date;
            }

            var filteredPayments = allPayments
                .Where(p => {
                    var localDate = p.CreatedAt.ToLocalTime().Date;
                    return localDate >= filterFromDate && localDate <= filterToDate;
                })
                .OrderByDescending(p => p.CreatedAt)
                .ToList();

            double filteredTotalBill = filteredPayments.Sum(p => p.Amount);
            double filteredTotalNetEarning = filteredPayments.Sum(p => {
                if (p.PaymentStatus == "Released" || p.PaymentStatus == "Completed" || p.PaymentStatus == "Paid")
                {
                    return p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount);
                }
                return 0;
            });

            var transactions = filteredPayments
                .Select(p => new {
                    id = p.Id,
                    jobId = p.JobId,
                    amount = p.Amount,
                    mechanicEarning = p.MechanicEarningAmount != 0 ? p.MechanicEarningAmount : (p.Amount - p.AdminCommissionAmount),
                    createdAt = p.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt"),
                    status = p.PaymentStatus
                })
                .ToList();

            return Json(new {
                success = true,
                todayEarnings = todayEarnings,
                weeklyEarnings = weeklyEarnings,
                monthlyEarnings = monthlyEarnings,
                monthlyVolume = monthlyEarnings, // for compatibility
                monthlyWithdrawal = monthlyWithdrawal,
                heldEarnings = heldEarnings,
                pendingSettlement = pendingSettlement,
                todayJobsCount = todayJobsCount,
                weeklyJobsCount = weeklyJobsCount,
                fromDate = filterFromDate.ToString("yyyy-MM-dd"),
                toDate = filterToDate.ToString("yyyy-MM-dd"),
                filteredTotalBill = filteredTotalBill,
                filteredTotalNetEarning = filteredTotalNetEarning,
                transactions = transactions
            });
        }

        [HttpGet]
        public async Task<IActionResult> CheckActiveJobOrPing()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false });

            var profile = await _dbContext.MechanicProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null) return Json(new { success = false });

            var activeJob = await _dbContext.Jobs.AsNoTracking()
                .FirstOrDefaultAsync(j => j.MechanicId == user.Id && j.Status != "Completed" && j.Status != "Cancelled");

            bool hasPing = false;
            int? pingJobId = null;
            if (profile.IsOnline && profile.KycStatus == "Approved" && activeJob == null)
            {
                var ping = await _dbContext.Jobs.AsNoTracking()
                    .Where(j => j.Status == "Requested" && j.MechanicId == null)
                    .OrderByDescending(j => j.CreatedAt)
                    .Select(j => new { j.Id, j.CreatedAt })
                    .FirstOrDefaultAsync();

                if (ping != null && (DateTime.UtcNow - ping.CreatedAt).TotalSeconds < 300)
                {
                    hasPing = true;
                    pingJobId = ping.Id;
                }
            }

            return Json(new
            {
                success = true,
                hasActiveJob = activeJob != null,
                activeJobId = activeJob?.Id,
                activeJobStatus = activeJob?.Status,
                hasPing = hasPing,
                pingJobId = pingJobId,
                walletBalance = profile.CurrentEarnings
            });
        }
    }
}
