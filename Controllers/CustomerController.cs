using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;
using RaahSathi.Services;

namespace RaahSathi.Controllers
{
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPricingEngine _pricingEngine;
        private readonly IDispatchEngine _dispatchEngine;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
        private readonly IPaymentService _paymentService;

        public CustomerController(ApplicationDbContext dbContext, IPricingEngine pricingEngine, IDispatchEngine dispatchEngine, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, IPaymentService paymentService)
        {
            _dbContext = dbContext;
            _pricingEngine = pricingEngine;
            _dispatchEngine = dispatchEngine;
            _env = env;
            _paymentService = paymentService;
        }

        private async Task<User?> GetActiveCustomerAsync()
        {
            if (User.Identity?.IsAuthenticated == true && (User.IsInRole("Customer") || User.IsInRole("Admin")))
            {
                string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int custId))
                {
                    return await _dbContext.Users.FindAsync(custId);
                }
            }

            return null;
        }

        private bool IsValidImageFile(IFormFile? file)
        {
            if (file == null || file.Length == 0) return false;
            var ext = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            if (!allowedExtensions.Contains(ext)) return false;

            var mime = file.ContentType.ToLowerInvariant();
            var allowedMimeTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
            if (!allowedMimeTypes.Contains(mime)) return false;

            return true;
        }

        public async Task<IActionResult> Dashboard()
        {
            var customer = await GetActiveCustomerAsync();
            if (customer == null) return RedirectToAction("Login", "Auth");

            var myVehicles = await _dbContext.Vehicles.Where(v => v.UserId == customer.Id).ToListAsync();
            var activeJobs = await _dbContext.Jobs
                .Include(j => j.Vehicle)
                .Include(j => j.Mechanic)
                .Where(j => j.CustomerId == customer.Id && j.Status != "Completed" && j.Status != "Cancelled")
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            var pastJobs = await _dbContext.Jobs
                .Include(j => j.Vehicle)
                .Include(j => j.Mechanic)
                .Where(j => j.CustomerId == customer.Id && (j.Status == "Completed" || j.Status == "Cancelled"))
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync();

            ViewBag.CustomerName = customer.Name;
            ViewBag.CustomerPhone = customer.PhoneNumber;
            ViewBag.Vehicles = myVehicles;
            ViewBag.ActiveJobs = activeJobs;
            ViewBag.PastJobs = pastJobs;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddVehicle(string vehicleType, string model, string? registrationNumber, IFormFile? vehiclePhoto)
        {
            var customer = await GetActiveCustomerAsync();
            if (customer == null) return RedirectToAction("Login", "Auth", new { role = "Customer", returnUrl = "/Customer/Dashboard?action=addVehicle" });

            if (string.IsNullOrWhiteSpace(model))
            {
                TempData["Error"] = "Please enter Vehicle Name & Model.";
                return RedirectToAction("Dashboard");
            }

            string photoUrl = string.Empty;
            if (vehiclePhoto != null && vehiclePhoto.Length > 0)
            {
                if (!IsValidImageFile(vehiclePhoto))
                {
                    TempData["Error"] = "Invalid file type. Only JPG, JPEG, and PNG images are allowed.";
                    return RedirectToAction("Dashboard");
                }

                try
                {
                    string uploadsFolder = System.IO.Path.Combine(_env.WebRootPath, "uploads", "vehicle_photos");
                    if (!System.IO.Directory.Exists(uploadsFolder))
                    {
                        System.IO.Directory.CreateDirectory(uploadsFolder);
                    }
                    string safeExtension = System.IO.Path.GetExtension(vehiclePhoto.FileName).ToLowerInvariant();
                    string uniqueFileName = Guid.NewGuid().ToString("N") + safeExtension;
                    string filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                    {
                        await vehiclePhoto.CopyToAsync(fileStream);
                    }
                    photoUrl = "/uploads/vehicle_photos/" + uniqueFileName;
                }
                catch { }
            }

            string regNum = !string.IsNullOrWhiteSpace(registrationNumber) 
                ? registrationNumber.Trim().ToUpper() 
                : ("UP16-RS-" + new Random().Next(1000, 9999));

            var vehicle = new Vehicle
            {
                UserId = customer.Id,
                VehicleType = string.IsNullOrWhiteSpace(vehicleType) ? "Car" : vehicleType,
                Model = model.Trim(),
                RegistrationNumber = regNum,
                VehiclePhotoUrl = photoUrl,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Vehicles.Add(vehicle);
            await _dbContext.SaveChangesAsync();

            TempData["Success"] = $"Vehicle '{model}' registered successfully!";
            return RedirectToAction("Dashboard");
        }

        public async Task<IActionResult> Book(int? selectedVehicleId = null)
        {
            var customer = await GetActiveCustomerAsync();
            var vehicles = new List<Vehicle>();

            if (customer != null)
            {
                vehicles = await _dbContext.Vehicles
                    .Where(v => v.UserId == customer.Id)
                    .ToListAsync();
            }

            ViewBag.Vehicles = vehicles;
            if (selectedVehicleId.HasValue && selectedVehicleId.Value > 0)
            {
                ViewBag.PreSelectedVehicle = vehicles.FirstOrDefault(v => v.Id == selectedVehicleId.Value);
            }
            ViewBag.PricingRules = await _dbContext.PricingRules.ToListAsync();
            ViewBag.ProblemTypes = await _dbContext.ProblemTypePricings.Where(p => p.IsActive).OrderBy(p => p.VehicleCategory).ThenBy(p => p.ProblemName).ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> QuickRequest(
            string vehicleType,
            string problemType,
            double lat,
            double lng,
            string address,
            string? landmark,
            string? fullName,
            string? phoneNumber,
            string? otpCode,
            int? vehicleId,
            string? vehicleNameModel,
            string? registrationNumber,
            string? problemDescription,
            IFormFile? problemPhoto)
        {
            User? customer = await GetActiveCustomerAsync();

            // If not logged in, perform instant verification and auto-registration
            if (customer == null)
            {
                if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length < 10)
                {
                    return Json(new { success = false, message = "Please enter a valid 10-digit mobile number." });
                }

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    return Json(new { success = false, message = "Please enter your full name." });
                }

                if (otpCode != "1234")
                {
                    return Json(new { success = false, message = "Invalid OTP code. Please use 1234 for instant verification." });
                }

                // Check or create user
                customer = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.Role == "Customer");
                if (customer == null)
                {
                    customer = new User
                    {
                        Name = fullName,
                        PhoneNumber = phoneNumber,
                        Role = "Customer",
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Users.Add(customer);
                    await _dbContext.SaveChangesAsync();
                }

                // Set login cookies
                var options = new CookieOptions
                {
                    Expires = DateTime.UtcNow.AddDays(30),
                    HttpOnly = true,
                    IsEssential = true
                };
                Response.Cookies.Append("RaahSathiCustomerUserId", customer.Id.ToString(), options);
                Response.Cookies.Append("RaahSathiUserName", customer.Name, options);
            }

            // Ensure vehicle exists for user
            Vehicle? vehicle = null;
            if (vehicleId.HasValue && vehicleId.Value > 0)
            {
                vehicle = await _dbContext.Vehicles.FindAsync(vehicleId.Value);
            }

            if (vehicle == null)
            {
                string defaultType = string.IsNullOrWhiteSpace(vehicleType) ? "Car" : vehicleType;
                vehicle = await _dbContext.Vehicles.FirstOrDefaultAsync(v => v.UserId == customer.Id && v.VehicleType == defaultType);
                if (vehicle == null)
                {
                    vehicle = new Vehicle
                    {
                        UserId = customer.Id,
                        VehicleType = defaultType,
                        Model = string.IsNullOrWhiteSpace(vehicleNameModel) ? $"{defaultType} Vehicle" : vehicleNameModel.Trim(),
                        RegistrationNumber = !string.IsNullOrWhiteSpace(registrationNumber) ? registrationNumber.Trim() : ("UP16-RS-" + new Random().Next(1000, 9999))
                    };
                    _dbContext.Vehicles.Add(vehicle);
                    await _dbContext.SaveChangesAsync();
                }
            }

            if (!string.IsNullOrWhiteSpace(vehicleNameModel))
            {
                vehicle.Model = vehicleNameModel.Trim();
            }
            if (!string.IsNullOrWhiteSpace(registrationNumber))
            {
                vehicle.RegistrationNumber = registrationNumber.Trim();
            }
            await _dbContext.SaveChangesAsync();

            string photoUrl = string.Empty;
            if (problemPhoto != null && problemPhoto.Length > 0)
            {
                if (!IsValidImageFile(problemPhoto))
                {
                    return Json(new { success = false, message = "Invalid file type. Only JPG, JPEG, and PNG images are allowed." });
                }

                try
                {
                    string uploadsFolder = System.IO.Path.Combine(_env.WebRootPath, "uploads", "problem_photos");
                    if (!System.IO.Directory.Exists(uploadsFolder))
                    {
                        System.IO.Directory.CreateDirectory(uploadsFolder);
                    }
                    string safeExtension = System.IO.Path.GetExtension(problemPhoto.FileName).ToLowerInvariant();
                    string uniqueFileName = Guid.NewGuid().ToString("N") + safeExtension;
                    string filePath = System.IO.Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                    {
                        await problemPhoto.CopyToAsync(fileStream);
                    }
                    photoUrl = "/uploads/problem_photos/" + uniqueFileName;
                }
                catch { }
            }

            string detectedCity = "";
            if (!string.IsNullOrWhiteSpace(address))
            {
                var matchedCity = await _dbContext.CityServiceAreas.FirstOrDefaultAsync(c => address.ToLower().Contains(c.CityName.ToLower()));
                if (matchedCity != null) detectedCity = matchedCity.CityName;
            }

            double mockDist = 3.5;
            var (baseFee, visitingCharge) = await _pricingEngine.CalculateVisitingChargeAsync(vehicle.VehicleType, mockDist);
            var (serviceMin, serviceMax) = _pricingEngine.GetServiceChargeRange(problemType ?? "Other", detectedCity);

            var job = new Job
            {
                CustomerId = customer.Id,
                VehicleId = vehicle.Id,
                ProblemType = string.IsNullOrWhiteSpace(problemType) ? "Breakdown Support" : problemType,
                Status = "Requested",
                FuelType = "Petrol",
                ProblemDescription = string.IsNullOrWhiteSpace(problemDescription) ? "30-Second Fast Request" : problemDescription,
                ProblemPhotoUrl = photoUrl,
                Landmark = landmark ?? "Current GPS Location",
                CustomerLat = lat > 0 ? lat : 28.6250,
                CustomerLng = lng > 0 ? lng : 77.3100,
                Address = string.IsNullOrWhiteSpace(address) ? "Current GPS Location" : address,
                VisitingCharge = visitingCharge,
                ServiceChargeMin = serviceMin,
                ServiceChargeMax = serviceMax,
                FinalBillAmount = Math.Round(visitingCharge + serviceMin, 2)
            };

            _dbContext.Jobs.Add(job);
            await _dbContext.SaveChangesAsync();

            // Get all ranked mechanics for tiered parallel dispatch with 5-star repeat preference
            var allRanked = await _dispatchEngine.FindAndRankMechanicsAsync(
                job.CustomerLat, job.CustomerLng, vehicle.VehicleType, job.ProblemType, customer.Id);

            var top5 = allRanked.Take(5).ToList();
            var preferredMechanic = allRanked.FirstOrDefault(m => m.Is5StarPreferred);

            return Json(new {
                success = true,
                jobId = job.Id,
                customerId = customer.Id,
                customerName = customer.Name,
                hasPreferred = preferredMechanic != null,
                preferredName = preferredMechanic?.Mechanic.Name,
                topMechanicsCount = top5.Count,
                totalMechanicsCount = allRanked.Count,
                topMechanic = top5.FirstOrDefault() != null ? new {
                    id = top5.First().Mechanic.Id,
                    name = top5.First().Mechanic.Name,
                    rating = top5.First().Profile.Rating,
                    eta = top5.First().EtaMinutes,
                    distance = top5.First().DistanceKm,
                    isPreferred = top5.First().Is5StarPreferred
                } : null
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetNearbyMechanicsInfo(double lat, double lng, string? vehicleType)
        {
            string type = string.IsNullOrWhiteSpace(vehicleType) ? "Car" : vehicleType;
            var ranked = await _dispatchEngine.FindAndRankMechanicsAsync(lat > 0 ? lat : 28.6250, lng > 0 ? lng : 77.3100, type);
            
            int count = ranked.Count;
            double rawMinEta = count > 0 ? ranked.Min(m => m.EtaMinutes) : 15.0;
            int minEta = (int)Math.Min(25.0, Math.Max(10.0, Math.Round(rawMinEta)));

            return Json(new {
                success = true,
                count = Math.Max(1, count),
                minEta = minEta,
                topMechanics = ranked.Take(3).Select(m => new {
                    name = m.Mechanic.Name,
                    rating = m.Profile.Rating,
                    distance = m.DistanceKm > 30.0 ? 2.8 : Math.Round(m.DistanceKm, 1),
                    eta = (int)Math.Min(25.0, Math.Max(10.0, m.EtaMinutes))
                })
            });
        }

        [HttpPost]
        public async Task<IActionResult> CreateRequest(int vehicleId, string problemType, double lat, double lng, string address, double mockDistance, string fuelType, string problemDescription, string landmark)
        {
            var customer = await GetActiveCustomerAsync();
            if (customer == null) return Json(new { success = false, message = "Session expired." });

            var vehicle = await _dbContext.Vehicles.FindAsync(vehicleId);
            if (vehicle == null) return Json(new { success = false, message = "Vehicle not found." });

            // Calculate distance server-side using nearest mechanic coordinates
            double serverDistance = 3.5;
            var rankedMechanics = await _dispatchEngine.FindAndRankMechanicsAsync(lat, lng, vehicle.VehicleType, problemType, customer.Id);
            if (rankedMechanics != null && rankedMechanics.Count > 0)
            {
                serverDistance = rankedMechanics.First().DistanceKm;
            }
            if (serverDistance <= 0) serverDistance = 0.1;

            // Calculate Upfront Prices
            var (baseFee, visitingCharge) = await _pricingEngine.CalculateVisitingChargeAsync(vehicle.VehicleType, serverDistance);
            var (serviceMin, serviceMax) = _pricingEngine.GetServiceChargeRange(problemType);

            // Create Job state: Requested
            var job = new Job
            {
                CustomerId = customer.Id,
                VehicleId = vehicleId,
                ProblemType = problemType,
                Status = "Requested",
                FuelType = string.IsNullOrEmpty(fuelType) ? "Petrol" : fuelType,
                ProblemDescription = problemDescription ?? string.Empty,
                Landmark = landmark ?? string.Empty,
                CustomerLat = lat,
                CustomerLng = lng,
                Address = string.IsNullOrEmpty(address) ? "Noida Sector 62, Highway NH24" : address,
                VisitingCharge = visitingCharge,
                ServiceChargeMin = serviceMin,
                ServiceChargeMax = serviceMax,
                FinalBillAmount = Math.Round(visitingCharge + serviceMin, 2)
            };

            _dbContext.Jobs.Add(job);
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, jobId = job.Id });
        }

        public async Task<IActionResult> GetDispatchOptions(int jobId)
        {
            var job = await _dbContext.Jobs
                .Include(j => j.Vehicle)
                .FirstOrDefaultAsync(j => j.Id == jobId);
            
            if (job == null) return NotFound();

            // Find scored and ranked mechanics with 5-star repeat customer priority
            var scoredMechanics = await _dispatchEngine.FindAndRankMechanicsAsync(
                job.CustomerLat, job.CustomerLng, job.Vehicle?.VehicleType ?? "Car", job.ProblemType, job.CustomerId);

            return Json(scoredMechanics.Select(m => new {
                m.Mechanic.Id,
                m.Mechanic.Name,
                m.Profile.Rating,
                m.Profile.ExperienceYears,
                m.Profile.TotalJobs,
                m.Profile.SkillCategory,
                m.DistanceKm,
                m.EtaMinutes,
                m.MatchScore,
                m.Is5StarPreferred,
                m.DistanceWeight,
                m.RatingWeight,
                m.SkillWeight,
                m.AvailabilityWeight,
                m.AcceptanceWeight,
                m.ActiveJobsCount
            }));
        }

        [HttpPost]
        public async Task<IActionResult> SimulateAcceptJob(int jobId, int mechanicId)
        {
            var job = await _dbContext.Jobs.FindAsync(jobId);
            var mechanic = await _dbContext.Users.FindAsync(mechanicId);
            
            if (job == null || mechanic == null) return Json(new { success = false, message = "Job or mechanic not found." });

            // Update job status to Accepted
            job.MechanicId = mechanicId;
            job.Status = "Accepted";
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true });
        }

        public async Task<IActionResult> Tracker(int id)
        {
            var customer = await GetActiveCustomerAsync();
            if (customer == null) return RedirectToAction("Login", "Auth");

            var job = await _dbContext.Jobs
                .Include(j => j.Vehicle)
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.Id == id && j.CustomerId == customer.Id);

            if (job == null) return NotFound();

            // Fetch mechanic profile for extra detail
            MechanicProfile? mechProfile = null;
            if (job.MechanicId.HasValue)
            {
                mechProfile = await _dbContext.MechanicProfiles
                    .FirstOrDefaultAsync(p => p.UserId == job.MechanicId.Value);
            }

            ViewBag.Job = job;
            ViewBag.MechanicProfile = mechProfile;

            // Load Admin Payment details for QR generation
            var upiSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminUpiId");
            var nameSetting = await _dbContext.AdminSystemSettings.FirstOrDefaultAsync(s => s.SettingKey == "AdminAccountHolderName");
            ViewBag.AdminUpiId = upiSetting?.SettingValue ?? "raahsathi@upi";
            ViewBag.AdminAccountHolderName = nameSetting?.SettingValue ?? "RaahSathi URA";

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetJobStatus(int id)
        {
            var job = await _dbContext.Jobs
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null) return NotFound();

            if (job.Status == "Requested" && (DateTime.UtcNow - job.CreatedAt).TotalSeconds >= 300)
            {
                job.Status = "TimedOut";
                await _dbContext.SaveChangesAsync();
            }

            MechanicProfile? mechProfile = null;
            if (job.MechanicId.HasValue)
            {
                await JobSimulationHelper.SimulateMovementAsync(_dbContext, job);
                mechProfile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == job.MechanicId.Value);
            }

            double inactiveSeconds = 0;
            if ((job.Status == "Accepted" || job.Status == "Driving") && job.LastMovementTime.HasValue)
            {
                inactiveSeconds = (DateTime.UtcNow - job.LastMovementTime.Value).TotalSeconds;
            }

            int unreadChatCount = 0;
            if (job.MechanicId.HasValue)
            {
                unreadChatCount = await _dbContext.JobChatMessages
                    .CountAsync(m => m.JobId == id && m.SenderRole == "Mechanic" && !m.IsRead);
            }

            return Json(new
            {
                jobId = job.Id,
                status = job.Status,
                mechanicId = job.MechanicId,
                mechanicName = job.Mechanic?.Name ?? "",
                mechanicPhone = job.Mechanic?.PhoneNumber ?? "",
                mechanicLat = mechProfile?.Latitude ?? 0,
                mechanicLng = mechProfile?.Longitude ?? 0,
                mechanicRating = mechProfile?.Rating ?? 5.0,
                mechanicTotalJobs = mechProfile?.TotalJobs ?? 0,
                partsEstimateAmount = job.PartsEstimateAmount,
                partsEstimateDetails = job.PartsEstimateDetails,
                extraPartsName = job.ExtraPartsName,
                extraLabourCharge = job.ExtraLabourCharge,
                partsApproved = job.PartsApproved,
                customEstimateAmount = job.CustomEstimateAmount,
                customEstimateDetails = job.CustomEstimateDetails,
                customEstimateApproved = job.CustomEstimateApproved,
                towingNeeded = job.TowingNeeded,
                towingCharge = job.TowingCharge,
                towingReason = job.TowingReason,
                towingProofPhoto = job.TowingProofPhoto,
                towingApproved = job.TowingApproved,
                finalBillAmount = job.FinalBillAmount,
                disputeStatus = job.DisputeStatus,
                customerLat = job.CustomerLat,
                customerLng = job.CustomerLng,
                isSimulationPaused = job.IsSimulationPaused,
                inactiveSeconds = inactiveSeconds,
                jobAgeSeconds = (DateTime.UtcNow - job.CreatedAt).TotalSeconds,
                unreadChatCount = unreadChatCount
            });
        }

        [HttpPost]
        public async Task<IActionResult> ReassignMechanic(int id)
        {
            var job = await _dbContext.Jobs
                .Include(j => j.Customer)
                .Include(j => j.Mechanic)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null) return NotFound();

            if (job.MechanicId.HasValue)
            {
                int oldMechanicId = job.MechanicId.Value;
                string oldMechIdStr = oldMechanicId.ToString();

                // Clear the declined list so the job rings for all online mechanics,
                // including the unassigned mechanic who was unassigned due to inactivity/lateness.
                job.DeclinedMechanicIds = "";
                job.CreatedAt = DateTime.UtcNow; // Reset creation time to renew search duration/progress bar

                job.MechanicId = null;
                job.Status = "Requested";
                job.IsSimulationPaused = false;
                job.LastMovementTime = null;
                job.LastLocationUpdateTime = null;

                var audit = new AuditLog
                {
                    AdminName = "System (Customer Request)",
                    ActionType = "REASSIGN",
                    Details = $"Job #{job.Id} reassigned. Mechanic ID {oldMechIdStr} was unassigned due to inactivity.",
                    TimeStamp = DateTime.UtcNow
                };
                _dbContext.AuditLogs.Add(audit);

                await _dbContext.SaveChangesAsync();
                return Json(new { success = true, message = "Mechanic successfully reassigned. Searching for new partners." });
            }

            return Json(new { success = false, message = "No mechanic was assigned to this job." });
        }

        [HttpPost]
        public async Task<IActionResult> RetryDispatch(int id)
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null) return NotFound();

            if (job.Status == "Requested" || job.Status == "TimedOut")
            {
                job.Status = "Requested";
                job.CreatedAt = DateTime.UtcNow; // Reset creation time to renew dispatch wails
                job.DeclinedMechanicIds = ""; // Reset declined list so previously declined mechanics can get it again
                await _dbContext.SaveChangesAsync();
                return Json(new { success = true, message = "Dispatch request successfully renewed!" });
            }

            return Json(new { success = false, message = "Job is no longer in pending dispatch state." });
        }

        [HttpPost]
        public async Task<IActionResult> KeepWaiting(int id)
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null) return NotFound();

            // Reset LastMovementTime to now to give the mechanic another chance
            job.LastMovementTime = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Timer reset. We notified the mechanic that you are waiting." });
        }

        [HttpPost]
        public async Task<IActionResult> ApproveCustomEstimate(int id, bool approve)
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null) return NotFound();

            job.CustomEstimateApproved = approve;
            if (approve)
            {
                job.Status = "Repairing";
            }
            else
            {
                job.Status = "Inspecting";
            }

            // Recalculate FinalBillAmount robustly
            double baseBill = job.VisitingCharge + job.ServiceChargeMin;
            double customEstimate = (job.CustomEstimateApproved == true) ? job.CustomEstimateAmount : 0.0;
            double partsCharge = (job.PartsApproved == true) ? (job.PartsEstimateAmount + job.ExtraLabourCharge) : 0.0;
            double towingCharge = (job.TowingApproved == true) ? job.TowingCharge : 0.0;
            job.FinalBillAmount = baseBill + customEstimate + partsCharge + towingCharge;

            await _dbContext.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ApproveParts(int id, bool approve)
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null) return NotFound();

            job.PartsApproved = approve;
            job.Status = "Repairing";

            // Recalculate FinalBillAmount robustly
            double baseBill = job.VisitingCharge + job.ServiceChargeMin;
            double customEstimate = (job.CustomEstimateApproved == true) ? job.CustomEstimateAmount : 0.0;
            double partsCharge = (job.PartsApproved == true) ? (job.PartsEstimateAmount + job.ExtraLabourCharge) : 0.0;
            double towingCharge = (job.TowingApproved == true) ? job.TowingCharge : 0.0;
            job.FinalBillAmount = baseBill + customEstimate + partsCharge + towingCharge;

            await _dbContext.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ApproveTowing(int id, bool approve)
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null) return NotFound();

            job.TowingApproved = approve;
            if (approve)
            {
                job.Status = "Completed"; // Towing finishes job instantly (towed to garage)
                job.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                job.Status = "Repairing";
            }

            // Recalculate FinalBillAmount robustly
            double baseBill = job.VisitingCharge + job.ServiceChargeMin;
            double customEstimate = (job.CustomEstimateApproved == true) ? job.CustomEstimateAmount : 0.0;
            double partsCharge = (job.PartsApproved == true) ? (job.PartsEstimateAmount + job.ExtraLabourCharge) : 0.0;
            double towingCharge = (job.TowingApproved == true) ? job.TowingCharge : 0.0;
            job.FinalBillAmount = baseBill + customEstimate + partsCharge + towingCharge;

            await _dbContext.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ProcessPayment(int id, string paymentId)
        {
            bool success = await _paymentService.ProcessEscrowPaymentForJobAsync(id, paymentId);
            if (!success) return NotFound();

            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetJobInvoiceDetails(int jobId)
        {
            var breakdown = await _paymentService.GenerateJobInvoiceBreakdownAsync(jobId);
            if (!breakdown.Success) return Json(new { success = false, message = breakdown.Message ?? "Job not found." });

            return Json(breakdown);
        }

        [HttpPost]
        public async Task<IActionResult> SubmitFeedback(
            int id, double rating, string? feedback, string? positiveTags, 
            bool? isRecommended, IFormFile? reviewPhoto,
            string? complaintReasons, string? complaintCategory, string? complaintDetails)
        {
            var customer = await GetActiveCustomerAsync();
            if (customer == null) return RedirectToAction("Login", "Auth", new { role = "Customer" });

            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null || job.CustomerId != customer.Id) return NotFound();

            // Fake Rating Protection: Only Completed Jobs can be rated
            if (job.Status != "Completed")
            {
                TempData["Error"] = "Only completed jobs can be rated.";
                return RedirectToAction("Dashboard");
            }

            // 24-48 Hours edit window check
            if (job.RatedAt.HasValue && (DateTime.UtcNow - job.RatedAt.Value).TotalHours > 48)
            {
                TempData["Error"] = "Rating edit window (48 hours) has expired for this booking.";
                return RedirectToAction("Dashboard");
            }

            // Process photo upload if provided
            string photoUrl = job.ReviewPhotoUrl ?? string.Empty;
            if (reviewPhoto != null && reviewPhoto.Length > 0)
            {
                if (!IsValidImageFile(reviewPhoto))
                {
                    TempData["Error"] = "Invalid file type. Only JPG, JPEG, and PNG images are allowed.";
                    return RedirectToAction("Dashboard");
                }

                try
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "reviews");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    string safeExtension = Path.GetExtension(reviewPhoto.FileName).ToLowerInvariant();
                    string uniqueFileName = Guid.NewGuid().ToString() + safeExtension;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await reviewPhoto.CopyToAsync(stream);
                    }
                    photoUrl = "/uploads/reviews/" + uniqueFileName;
                }
                catch { }
            }

            job.RatingFromCustomer = rating;
            job.FeedbackFromCustomer = feedback ?? string.Empty;
            job.PositiveFeedbackTags = positiveTags ?? string.Empty;
            job.IsRecommended = isRecommended;
            job.ReviewPhotoUrl = photoUrl;
            job.RatedAt = DateTime.UtcNow;

            // Update mechanic metrics
            if (job.MechanicId.HasValue)
            {
                var profile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == job.MechanicId.Value);
                if (profile != null)
                {
                    var ratedJobs = await _dbContext.Jobs
                        .Where(j => j.MechanicId == job.MechanicId.Value && j.RatingFromCustomer.HasValue)
                        .ToListAsync();

                    int totalRevs = ratedJobs.Count;
                    if (!ratedJobs.Any(j => j.Id == job.Id)) totalRevs += 1;

                    double sumRatings = ratedJobs.Sum(j => j.RatingFromCustomer!.Value);
                    if (!ratedJobs.Any(j => j.Id == job.Id)) sumRatings += rating;

                    int recommendedCount = ratedJobs.Count(j => j.IsRecommended == true);
                    if (isRecommended == true && !ratedJobs.Any(j => j.Id == job.Id)) recommendedCount += 1;

                    profile.TotalReviewsCount = Math.Max(1, totalRevs);
                    profile.RecommendedCount = recommendedCount;
                    profile.Rating = Math.Round(sumRatings / profile.TotalReviewsCount, 1);
                    profile.RecommendationPercentage = (int)Math.Round(((double)profile.RecommendedCount / profile.TotalReviewsCount) * 100);
                }

                // Escalate to Admin as Complaint if rating is less than 4 stars
                if (rating < 4)
                {
                    var existingComplaint = await _dbContext.MechanicComplaints
                        .FirstOrDefaultAsync(c => c.JobId == job.Id);

                    if (existingComplaint == null)
                    {
                        var complaint = new MechanicComplaint
                        {
                            JobId = job.Id,
                            CustomerId = job.CustomerId,
                            MechanicId = job.MechanicId.Value,
                            Rating = rating,
                            SelectedReasons = complaintReasons ?? string.Empty,
                            Category = string.IsNullOrEmpty(complaintCategory) ? "General" : complaintCategory,
                            CustomerDetails = string.IsNullOrEmpty(complaintDetails) ? (feedback ?? "") : complaintDetails,
                            Status = "Pending",
                            CreatedAt = DateTime.UtcNow
                        };
                        _dbContext.MechanicComplaints.Add(complaint);
                    }
                }
            }

            await _dbContext.SaveChangesAsync();
            TempData["Success"] = rating >= 4 
                ? "Thank you for your rating & review!" 
                : "Your feedback & complaint have been recorded and escalated to Admin for review.";
            return RedirectToAction("Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> FileDispute(int id, string reason)
        {
            var job = await _dbContext.Jobs.FindAsync(id);
            if (job == null) return NotFound();

            job.DisputeStatus = "Active";
            job.DisputeReason = reason;

            await _dbContext.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitComplaint(int jobId, string mechanicName, string location, string problem, string details)
        {
            var user = await GetActiveCustomerAsync();
            if (user == null) return Json(new { success = false, message = "Not authenticated" });

            var job = await _dbContext.Jobs.FindAsync(jobId);
            if (job == null || job.CustomerId != user.Id)
            {
                return Json(new { success = false, message = "Job record not found or does not belong to your account." });
            }

            job.DisputeStatus = "Active";
            job.DisputeReason = $"[Reported Mechanic: {mechanicName}] [Location: {location}] [Problem Type: {problem}] [Complaint Details: {details}]";

            await _dbContext.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> GetChatMessages(int jobId, string? viewerRole = null)
        {
            var messages = await _dbContext.JobChatMessages
                .Where(m => m.JobId == jobId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            if (!string.IsNullOrEmpty(viewerRole))
            {
                bool changed = false;
                foreach (var msg in messages)
                {
                    if (viewerRole == "Customer" && msg.SenderRole == "Mechanic" && !msg.IsRead)
                    {
                        msg.IsRead = true;
                        changed = true;
                    }
                    else if (viewerRole == "Mechanic" && msg.SenderRole == "Customer" && !msg.IsRead)
                    {
                        msg.IsRead = true;
                        changed = true;
                    }
                }
                if (changed)
                {
                    await _dbContext.SaveChangesAsync();
                }
            }

            var result = messages.Select(m => new {
                id = m.Id,
                senderRole = m.SenderRole,
                senderName = m.SenderName,
                messageText = m.MessageText,
                sentTime = m.SentAt.ToLocalTime().ToString("hh:mm tt")
            }).ToList();

            return Json(new { success = true, messages = result });
        }

        [HttpPost]
        public async Task<IActionResult> SendChatMessage(int jobId, string message, string role = "Customer")
        {
            if (string.IsNullOrWhiteSpace(message)) return Json(new { success = false, message = "Empty text" });

            var job = await _dbContext.Jobs.Include(j => j.Customer).Include(j => j.Mechanic).FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return Json(new { success = false, message = "Job not found" });

            string senderName = role == "Mechanic" 
                ? (job.Mechanic?.Name ?? "Mechanic") 
                : (job.Customer?.Name ?? "Customer");
            
            int senderId = role == "Mechanic" ? (job.MechanicId ?? 0) : job.CustomerId;

            var chatMsg = new JobChatMessage
            {
                JobId = jobId,
                SenderId = senderId,
                SenderRole = role,
                SenderName = senderName,
                MessageText = message.Trim(),
                SentAt = DateTime.UtcNow
            };

            _dbContext.JobChatMessages.Add(chatMsg);
            await _dbContext.SaveChangesAsync();

            return Json(new { 
                success = true, 
                msg = new {
                    id = chatMsg.Id,
                    senderRole = chatMsg.SenderRole,
                    senderName = chatMsg.SenderName,
                    messageText = chatMsg.MessageText,
                    sentTime = chatMsg.SentAt.ToLocalTime().ToString("hh:mm tt")
                }
            });
        }
    }
}
