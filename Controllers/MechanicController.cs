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

        public MechanicController(ApplicationDbContext dbContext, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, Services.IDispatchEngine dispatchEngine, Services.IPaymentService paymentService)
        {
            _dbContext = dbContext;
            _env = env;
            _dispatchEngine = dispatchEngine;
            _paymentService = paymentService;
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

            // Calculate Wallet Stats
            var payments = await _dbContext.Payments
                .Include(p => p.Job)
                .Where(p => p.Job != null && p.Job.MechanicId == user.Id && p.PaymentStatus == "Released")
                .ToListAsync();

            var todayLocal = DateTime.UtcNow.ToLocalTime().Date;
            double todayEarnings = payments
                .Where(p => p.CreatedAt.ToLocalTime().Date == todayLocal)
                .Sum(p => p.MechanicEarningAmount);

            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            double weeklyEarnings = payments
                .Where(p => p.CreatedAt >= sevenDaysAgo)
                .Sum(p => p.MechanicEarningAmount);

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            double monthlyVolume = payments
                .Where(p => p.CreatedAt >= thirtyDaysAgo)
                .Sum(p => p.Amount);

            double pendingSettlement = profile.CurrentEarnings;

            ViewBag.User = user;
            ViewBag.Profile = profile;
            ViewBag.ActiveJob = activeJob;
            ViewBag.PingJob = pingJob;
            ViewBag.PastJobs = pastJobs;
            ViewBag.ActiveWarning = activeWarning;
            ViewBag.SupportMessages = supportMessages;
            ViewBag.UnreadSupportCount = supportMessages.Count(m => !m.IsRead && m.IsFromAdmin);
            ViewBag.TodayEarnings = todayEarnings;
            ViewBag.WeeklyEarnings = weeklyEarnings;
            ViewBag.MonthlyVolume = monthlyVolume;
            ViewBag.PendingSettlement = pendingSettlement;
            ViewBag.Payments = payments.OrderByDescending(p => p.CreatedAt).ToList();

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
        public async Task<IActionResult> GetKycStatus()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.FindAsync(user.Id);
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

            var profile = await _dbContext.MechanicProfiles.FindAsync(user.Id);

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
                        estEarningsMin = (int)Math.Round((job.VisitingCharge + job.ServiceChargeMin) * (1 - profile.CommissionRate)),
                        estEarningsMax = (int)Math.Round((job.VisitingCharge + job.ServiceChargeMax) * (1 - profile.CommissionRate)),
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

        [HttpGet]
        public async Task<IActionResult> GetJobInvoiceDetails(int jobId)
        {
            var job = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Vehicle)
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null) return Json(new { success = false, message = "Job not found." });

            var mechProfile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == job.MechanicId);
            var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.JobId == jobId);

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
                commissionPercent = effectiveCommRatePct
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

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null || job.MechanicId != user.Id) return Json(new { success = false, message = "Job not found" });

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
                finalBillAmount = job.FinalBillAmount
            });
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

        [HttpGet]
        public async Task<IActionResult> GetWalletStats()
        {
            var user = await GetActiveMechanicUserAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile == null) return Json(new { success = false, message = "Profile not found" });

            var payments = await _dbContext.Payments
                .Include(p => p.Job)
                .Where(p => p.Job != null && p.Job.MechanicId == user.Id && p.PaymentStatus == "Released")
                .ToListAsync();

            var todayLocal = DateTime.UtcNow.ToLocalTime().Date;
            double todayEarnings = payments
                .Where(p => p.CreatedAt.ToLocalTime().Date == todayLocal)
                .Sum(p => p.MechanicEarningAmount);

            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            double weeklyEarnings = payments
                .Where(p => p.CreatedAt >= sevenDaysAgo)
                .Sum(p => p.MechanicEarningAmount);

            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            double monthlyVolume = payments
                .Where(p => p.CreatedAt >= thirtyDaysAgo)
                .Sum(p => p.Amount);

            double pendingSettlement = profile.CurrentEarnings;

            var transactions = payments
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .Select(p => new {
                    id = p.Id,
                    jobId = p.JobId,
                    amount = p.Amount,
                    mechanicEarning = p.MechanicEarningAmount,
                    createdAt = p.CreatedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt"),
                    status = p.PaymentStatus
                })
                .ToList();

            return Json(new {
                success = true,
                todayEarnings = todayEarnings,
                weeklyEarnings = weeklyEarnings,
                monthlyVolume = monthlyVolume,
                pendingSettlement = pendingSettlement,
                transactions = transactions
            });
        }
    }
}
