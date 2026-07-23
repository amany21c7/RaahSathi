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

        public MechanicController(ApplicationDbContext dbContext, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, Services.IDispatchEngine dispatchEngine)
        {
            _dbContext = dbContext;
            _env = env;
            _dispatchEngine = dispatchEngine;
        }

        private async Task<User?> GetActiveMechanicUserAsync()
        {
            string? mechIdStr = Request.Cookies["RaahSathiMechanicUserId"];
            if (!string.IsNullOrEmpty(mechIdStr) && int.TryParse(mechIdStr, out int mechId))
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

            string? role = Request.Cookies["RaahSathiUserRole"];
            string? userIdStr = Request.Cookies["RaahSathiUserId"];

            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
            {
                var user = await _dbContext.Users.FindAsync(userId);
                if (user != null)
                {
                    if (role == "Mechanic" && user.Role != "Mechanic" && user.Role != "Admin")
                    {
                        user.Role = "Mechanic";
                        await _dbContext.SaveChangesAsync();
                    }
                    return user;
                }
            }
 
            return null;
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

            // Check if there is an unassigned "Requested" job nearby that fits this mechanic's skills
            // To simulate incoming dispatch pings in the UI
            Job? pingJob = null;
            if (profile.IsOnline && profile.KycStatus == "Approved" && activeJob == null)
            {
                pingJob = await _dbContext.Jobs
                    .Include(j => j.Customer)
                    .Include(j => j.Vehicle)
                    .FirstOrDefaultAsync(j => j.Status == "Requested" && j.MechanicId == null);
            }

            // Historical Jobs
            var pastJobs = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .Where(j => j.MechanicId == user.Id && j.Status == "Completed")
                .OrderByDescending(j => j.CompletedAt)
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

            ViewBag.User = user;
            ViewBag.Profile = profile;
            ViewBag.ActiveJob = activeJob;
            ViewBag.PingJob = pingJob;
            ViewBag.PastJobs = pastJobs;
            ViewBag.ActiveWarning = activeWarning;
            ViewBag.SupportMessages = supportMessages;
            ViewBag.UnreadSupportCount = supportMessages.Count(m => !m.IsRead && m.IsFromAdmin);

            return View();
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
        public async Task<IActionResult> KycForm()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return RedirectToAction("Login", "Auth");

            var profile = await _dbContext.MechanicProfiles.FindAsync(user.Id);

            ViewBag.UserName = user.Name;
            ViewBag.UserPhone = user.PhoneNumber;
            ViewBag.KycStatus = profile?.KycStatus ?? "Incomplete";
            ViewBag.Profile = profile;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitKycForm(
            string Email, DateTime? DateOfBirth, string Gender, IFormFile ProfilePhoto,
            string AadhaarNumber, IFormFile AadhaarFrontPhoto, IFormFile AadhaarBackPhoto, IFormFile PanCardPhoto, IFormFile SelfiePhoto,
            string ShopName, string ShopAddress, string Pincode, string ShopTiming, IFormFile ShopPhoto,
            int ExperienceYears, bool IsCertified, string GarageName,
            string[] VehicleExpertise, string[] Specialization, int ServiceRadiusKm)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return RedirectToAction("Login", "Auth");

            var profile = await _dbContext.MechanicProfiles.FindAsync(user.Id);
            if (profile == null)
            {
                profile = new MechanicProfile { UserId = user.Id };
                _dbContext.MechanicProfiles.Add(profile);
            }

            // Helper to save files
            async Task<string> SaveFileAsync(IFormFile file)
            {
                if (file == null || file.Length == 0) return "";
                var uploadsFolder = System.IO.Path.Combine(_env.WebRootPath, "uploads");
                System.IO.Directory.CreateDirectory(uploadsFolder);
                var uniqueName = Guid.NewGuid().ToString() + "_" + file.FileName;
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
            profile.Specialization = Specialization != null ? string.Join(", ", Specialization) : (profile.Specialization ?? "");
            
            // Map legacy SkillCategory based on VehicleExpertise
            profile.SkillCategory = !string.IsNullOrEmpty(profile.VehicleExpertise) ? profile.VehicleExpertise : "Car"; 

            if (ProfilePhoto != null) profile.ProfilePhotoUrl = await SaveFileAsync(ProfilePhoto);
            if (AadhaarFrontPhoto != null) profile.AadhaarFrontUrl = await SaveFileAsync(AadhaarFrontPhoto);
            if (AadhaarBackPhoto != null) profile.AadhaarBackUrl = await SaveFileAsync(AadhaarBackPhoto);
            if (PanCardPhoto != null) profile.PanCardUrl = await SaveFileAsync(PanCardPhoto);
            if (SelfiePhoto != null) profile.SelfieUrl = await SaveFileAsync(SelfiePhoto);
            if (ShopPhoto != null) profile.ShopPhotoUrl = await SaveFileAsync(ShopPhoto);

            // Ensure no string property is null for SQL Server NOT NULL constraints
            profile.ProfilePhotoUrl ??= "";
            profile.AadhaarFrontUrl ??= "";
            profile.AadhaarBackUrl ??= "";
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

            var profile = await _dbContext.MechanicProfiles.FindAsync(user.Id);
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

            var profile = await _dbContext.MechanicProfiles.FindAsync(user.Id);
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
            if (user == null) return RedirectToAction("Login", "Auth");

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return Json(new { success = false, message = "Job not found." });

            if (job.MechanicId != null && job.MechanicId != user.Id)
            {
                return Json(new { success = false, message = "Job has already been accepted by another mechanic." });
            }

            job.MechanicId = user.Id;
            job.Status = "Accepted";
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> CheckIncomingDispatch(int? currentAlertJobId)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null || !profile.IsOnline || profile.KycStatus != "Approved")
            {
                return Json(new { success = true, hasJob = false });
            }

            // Check if mechanic already has an assigned active job
            var activeJob = await _dbContext.Jobs
                .FirstOrDefaultAsync(j => j.MechanicId == user.Id && j.Status != "Completed" && j.Status != "Cancelled");

            if (activeJob != null)
            {
                return Json(new { success = true, hasActiveJob = true, activeJobId = activeJob.Id });
            }

            // Check if the current alert job was accepted by another mechanic
            if (currentAlertJobId.HasValue)
            {
                var trackedJob = await _dbContext.Jobs.FindAsync(currentAlertJobId.Value);
                if (trackedJob == null || (trackedJob.MechanicId != null && trackedJob.MechanicId != user.Id) || trackedJob.Status != "Requested")
                {
                    return Json(new { success = true, hasJob = false, wasTaken = true, takenMessage = "This job request has been accepted by another mechanic." });
                }
            }

            // Search for unassigned "Requested" jobs within 15 km radius
            var requestedJobs = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .Where(j => j.Status == "Requested" && j.MechanicId == null)
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            string userStrId = user.Id.ToString();

            foreach (var job in requestedJobs)
            {
                // Skip if mechanic previously declined this job
                if (!string.IsNullOrEmpty(job.DeclinedMechanicIds))
                {
                    var declinedIds = job.DeclinedMechanicIds.Split(',').Select(id => id.Trim()).ToList();
                    if (declinedIds.Contains(userStrId)) continue;
                }

                // Dynamic radius expansion based on job age (0-20s: 15km, 20-30s: 30km, 30s+: 50km)
                double jobAgeSeconds = (DateTime.UtcNow - job.CreatedAt).TotalSeconds;
                double maxRadiusKm = jobAgeSeconds < 20 ? 15.0 : (jobAgeSeconds < 30 ? 30.0 : 50.0);

                // Check distance within expanding radius limit
                double distanceKm = _dispatchEngine.CalculateDistance(job.CustomerLat, job.CustomerLng, profile.Latitude, profile.Longitude);
                if (distanceKm <= maxRadiusKm)
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
                        estEarningsMin = (int)(job.VisitingCharge + job.ServiceChargeMin),
                        estEarningsMax = (int)(job.VisitingCharge + job.ServiceChargeMax + 150),
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
                if (string.IsNullOrEmpty(job.DeclinedMechanicIds))
                {
                    job.DeclinedMechanicIds = userStrId;
                }
                else
                {
                    var ids = job.DeclinedMechanicIds.Split(',').Select(i => i.Trim()).ToList();
                    if (!ids.Contains(userStrId))
                    {
                        job.DeclinedMechanicIds += "," + userStrId;
                    }
                }
                await _dbContext.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> AdvanceJobStatus(int jobId, string newStatus)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return NotFound();

            job.Status = newStatus;
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
        public async Task<IActionResult> SubmitSparesQuote(int jobId, string partName, double partPrice, double labourPrice)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null) return NotFound();

            job.ExtraPartsName = partName;
            job.PartsEstimateAmount = partPrice;
            job.ExtraLabourCharge = labourPrice;
            job.PartsEstimateDetails = $"{partName} (₹{partPrice}) + Labour (₹{labourPrice})";
            job.PartsApproved = null; // Reset to pending customer approval
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
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
            string[] vehicleExpertise, string[] specialization, int serviceRadiusKm, 
            string languages, string workingHours, string bankName, string accountNumber, string ifscCode, string upiId,
            string preferredPayoutMethod, bool acceptsCash = true)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.FindAsync(user.Id);
            if (profile != null)
            {
                if (vehicleExpertise != null && vehicleExpertise.Length > 0)
                    profile.VehicleExpertise = string.Join(", ", vehicleExpertise);

                if (specialization != null && specialization.Length > 0)
                    profile.Specialization = string.Join(", ", specialization);

                if (serviceRadiusKm > 0) profile.ServiceRadiusKm = serviceRadiusKm;
                profile.Languages = string.IsNullOrEmpty(languages) ? "Hindi, English" : languages;
                profile.WorkingHours = string.IsNullOrEmpty(workingHours) ? "9:00 AM - 9:00 PM" : workingHours;

                // All payment fields are completely optional (Bank, UPI, or Cash)
                profile.BankName = bankName ?? string.Empty;
                profile.BankAccountNumber = accountNumber ?? string.Empty;
                profile.IfscCode = ifscCode ?? string.Empty;
                profile.UpiId = upiId ?? string.Empty;
                profile.PreferredPayoutMethod = string.IsNullOrEmpty(preferredPayoutMethod) ? "UPI" : preferredPayoutMethod;
                profile.AcceptsCash = acceptsCash;

                await _dbContext.SaveChangesAsync();
                return Json(new { success = true, message = "Profile & payment preferences saved successfully!" });
            }

            return Json(new { success = false, message = "Profile not found." });
        }

        [HttpPost]
        public async Task<IActionResult> WithdrawWallet(double amount)
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.FindAsync(user.Id);
            if (profile != null)
            {
                if (amount <= 0 || amount > profile.CurrentEarnings)
                {
                    return Json(new { success = false, message = "Invalid withdrawal amount." });
                }

                profile.CurrentEarnings -= amount;
                await _dbContext.SaveChangesAsync();
                return Json(new { success = true, remainingBalance = profile.CurrentEarnings, message = $"₹{amount:F2} successfully transferred to your linked account!" });
            }

            return Json(new { success = false, message = "Profile not found." });
        }

        [HttpPost]
        public async Task<IActionResult> InstantPayout()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return RedirectToAction("Login", "Auth");

            var profile = await _dbContext.MechanicProfiles.FindAsync(user.Id);
            if (profile != null && profile.CurrentEarnings > 0)
            {
                double amount = profile.CurrentEarnings;
                profile.CurrentEarnings = 0; // Payout wallet reset
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
    }
}
