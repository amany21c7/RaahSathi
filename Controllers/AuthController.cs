using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using RaahSathi.Services;

namespace RaahSathi.Controllers
{
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth-policy")]
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _env;
        private readonly IAuthService _authService;
        private readonly IReferralService _referralService;
        private readonly IWhatsAppOtpService _whatsAppOtpService;

        public AuthController(
            ApplicationDbContext dbContext,
            IWebHostEnvironment env,
            IAuthService authService,
            IReferralService referralService,
            IWhatsAppOtpService whatsAppOtpService)
        {
            _dbContext = dbContext;
            _env = env;
            _authService = authService;
            _referralService = referralService;
            _whatsAppOtpService = whatsAppOtpService;
        }

        private async Task SetUserCookies(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("AdminRole", string.IsNullOrWhiteSpace(user.AdminRole) ? "Super Admin" : user.AdminRole)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

            // Set secure cookies for front-end/view compatibility
            var options = new CookieOptions { Expires = DateTime.UtcNow.AddDays(30), HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Secure = HttpContext.Request.IsHttps };
            Response.Cookies.Append("RaahSathiUserRole", user.Role, options);
            Response.Cookies.Append("RaahSathiUserId", user.Id.ToString(), options);
            Response.Cookies.Append("RaahSathiUserName", user.Name, new CookieOptions { Expires = DateTime.UtcNow.AddDays(30), HttpOnly = false, IsEssential = true, SameSite = SameSiteMode.Lax, Secure = HttpContext.Request.IsHttps });

            if (user.Role == "Customer")
            {
                Response.Cookies.Append("RaahSathiCustomerUserId", user.Id.ToString(), options);
            }
            else if (user.Role == "Mechanic")
            {
                Response.Cookies.Append("RaahSathiMechanicUserId", user.Id.ToString(), options);
            }
            else if (user.Role == "Admin")
            {
                Response.Cookies.Append("RaahSathiAdminUserId", user.Id.ToString(), options);
                Response.Cookies.Append("RaahSathiAdminRole", string.IsNullOrWhiteSpace(user.AdminRole) ? "Super Admin" : user.AdminRole, options);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? role, string? switchRole, string? @ref)
        {
            string? targetRole = role ?? switchRole;
            bool isAdminRequest = string.Equals(targetRole, "Admin", StringComparison.OrdinalIgnoreCase);

            if (isAdminRequest)
            {
                if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }

                if (User.Identity?.IsAuthenticated == true)
                {
                    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }

                ViewBag.TargetRole = "Admin";
                ViewBag.ReferralCode = @ref ?? string.Empty;
                return View();
            }

            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Mechanic"))
                {
                    return RedirectToAction("Dashboard", "Mechanic");
                }
                if (User.IsInRole("Customer"))
                {
                    return RedirectToAction("Dashboard", "Customer");
                }
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction("Dashboard", "Admin");
                }
            }
            else
            {
                // Clear stale legacy cookies if claims session is not authenticated
                Response.Cookies.Delete("RaahSathiCustomerUserId");
                Response.Cookies.Delete("RaahSathiMechanicUserId");
                Response.Cookies.Delete("RaahSathiAdminUserId");
                Response.Cookies.Delete("RaahSathiUserRole");
                Response.Cookies.Delete("RaahSathiUserId");
                Response.Cookies.Delete("RaahSathiUserName");
            }

            ViewBag.TargetRole = targetRole;
            ViewBag.ReferralCode = @ref ?? string.Empty;
            return View();
        }

        [HttpGet]
        [Route("AdminRaahiSathiLogin")]
        [Route("AdminRahiSarhiLogin")]
        [Route("AdminRahiSathiLogin")]
        [Route("AdminRaahSathiLogin")]
        public async Task<IActionResult> AdminRahiSarhiLogin()
        {
            if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
            {
                return RedirectToAction("Dashboard", "Admin");
            }

            if (User.Identity?.IsAuthenticated == true)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }

            Response.Cookies.Delete("RaahSathiCustomerUserId");
            Response.Cookies.Delete("RaahSathiMechanicUserId");
            Response.Cookies.Delete("RaahSathiAdminUserId");
            Response.Cookies.Delete("RaahSathiUserRole");
            Response.Cookies.Delete("RaahSathiUserId");
            Response.Cookies.Delete("RaahSathiUserName");

            ViewBag.TargetRole = "Admin";
            return View("Login");
        }

        private static string CleanPhoneNumber(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 12 && digits.StartsWith("91"))
            {
                digits = digits.Substring(2);
            }
            return digits;
        }

        [HttpPost]
        public async Task<IActionResult> SendOtp(string phoneNumber, string role)
        {
            string cleanPhone = CleanPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                return Json(new { success = false, message = "Please enter a valid 10-digit mobile number." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && (string.IsNullOrEmpty(role) || u.Role == role));
            if (user == null)
            {
                return Json(new { success = false, message = "Mobile number is not registered. Please register first." });
            }

            var otpResult = await _whatsAppOtpService.SendOtpAsync(cleanPhone, "Login", allowEmergencyFallback: false);
            return Json(new { success = otpResult.Success, message = otpResult.Message });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtp(string phoneNumber, string role, string otp)
        {
            string cleanPhone = CleanPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                return Json(new { success = false, message = "Please enter a valid 10-digit mobile number." });
            }

            var verifyResult = _whatsAppOtpService.VerifyOtp(cleanPhone, otp);
            if (!verifyResult.Success)
            {
                return Json(new { success = false, message = verifyResult.Message });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && (string.IsNullOrEmpty(role) || u.Role == role));
            if (user == null)
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone));
            }

            if (user == null)
            {
                return Json(new { success = false, message = "User account not found." });
            }

            _whatsAppOtpService.ClearPhoneVerification(cleanPhone);
            await SetUserCookies(user);

            string redirectUrl = user.Role == "Customer" ? "/Customer/Dashboard" 
                               : user.Role == "Mechanic" ? "/Mechanic/Dashboard" 
                               : "/Admin/Dashboard";

            return Json(new { success = true, redirect = redirectUrl });
        }


        [HttpPost]
        public async Task<IActionResult> PasswordLogin(string phoneNumber, string password, string? role)
        {
            if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(password))
            {
                return Json(new { success = false, message = "Please enter both mobile number and password." });
            }

            string cleanPhone = CleanPhoneNumber(phoneNumber);
            password = password.Trim();

            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                return Json(new { success = false, message = "Please enter a valid 10-digit mobile number." });
            }

            // Find user by phone number and role
            RaahSathi.Models.User? user;
            if (role == "Admin")
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == "Admin");
                if (user == null)
                {
                    return Json(new { success = false, message = "Access Denied: This mobile number is not authorized as an Administrator." });
                }
            }
            else
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == role);
                if (user == null)
                {
                    user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone));
                }

                if (user == null)
                {
                    return Json(new { success = false, message = "Mobile number is not registered. Please click 'Create Account' to sign up." });
                }

                // Verify role matching
                if (!string.IsNullOrEmpty(role) && user.Role != role && user.Role != "Admin")
                {
                    return Json(new { success = false, message = $"This phone number is registered as a {user.Role}. Please select the {user.Role} option to log in." });
                }
            }


            // Check if user has no password set (created via Guest Booking or quick OTP flow)
            if (string.IsNullOrWhiteSpace(user.Password) || user.Password == "OTP_USER_123")
            {
                return Json(new { success = false, message = "No password has been set for this account yet. Please click 'Forgot Password?' to create your password." });
            }

            // Verify password
            bool isPasswordValid = PasswordHasher.VerifyPassword(password, user.Password) || user.Password == password;
            if (!isPasswordValid)
            {
                return Json(new { success = false, message = "Incorrect password. Please try again or click 'Forgot Password?' to reset." });
            }

            await SetUserCookies(user);

            string redirectUrl = user.Role == "Customer" ? "/Customer/Dashboard" 
                               : user.Role == "Mechanic" ? "/Mechanic/Dashboard" 
                               : "/Admin/Dashboard";

            return Json(new { success = true, redirect = redirectUrl });
        }

        [HttpPost]
        public async Task<IActionResult> SendOtpForRegistration(string phoneNumber, string role, string name)
        {
            string cleanPhone = CleanPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                return Json(new { success = false, message = "Please enter a valid 10-digit mobile number." });
            }
            if (string.IsNullOrEmpty(name))
            {
                return Json(new { success = false, message = "Please enter your full name." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == role);
            if (user != null)
            {
                // If user already exists AND has a password set, guide them to login
                if (!string.IsNullOrWhiteSpace(user.Password) && user.Password != "OTP_USER_123")
                {
                    return Json(new { success = false, message = "Mobile number is already registered. Please login with your password or use 'Forgot Password?'." });
                }
            }

            var otpResult = await _whatsAppOtpService.SendOtpAsync(cleanPhone, "Registration", allowEmergencyFallback: false);
            return Json(new { success = otpResult.Success, message = otpResult.Message });
        }

        [HttpPost]
        public IActionResult VerifyOtpForRegistration(string phoneNumber, string otp)
        {
            string cleanPhone = CleanPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                return Json(new { success = false, message = "Please enter a valid 10-digit mobile number." });
            }

            var verifyResult = _whatsAppOtpService.VerifyOtp(cleanPhone, otp);
            return Json(new { success = verifyResult.Success, message = verifyResult.Message });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteRegistration(string name, string phoneNumber, string role, string password, string? referralCode)
        {
            string cleanPhone = CleanPhoneNumber(phoneNumber);

            if (!_whatsAppOtpService.IsPhoneVerified(cleanPhone))
            {
                return Json(new { success = false, message = "WhatsApp number verification is required. Please verify your OTP first." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == role);
            
            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(user.Password) && user.Password != "OTP_USER_123")
                {
                    return Json(new { success = false, message = "Mobile number is already registered. Please login." });
                }

                // Update existing unpassworded account (from guest booking) with new password and name
                user.Name = name.Trim();
                user.Password = PasswordHasher.HashPassword(password);
                await _dbContext.SaveChangesAsync();

                await _referralService.EnsureUserReferralCodeAsync(user.Id);
                if (!string.IsNullOrWhiteSpace(referralCode))
                {
                    await _referralService.RegisterReferralSignupAsync(user.Id, referralCode);
                }
            }
            else
            {
                user = new User
                {
                    Name = name.Trim(),
                    PhoneNumber = cleanPhone,
                    Role = role,
                    Password = PasswordHasher.HashPassword(password),
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                await _referralService.EnsureUserReferralCodeAsync(user.Id);
                if (!string.IsNullOrWhiteSpace(referralCode))
                {
                    await _referralService.RegisterReferralSignupAsync(user.Id, referralCode);
                }

                if (role == "Mechanic")
                {
                    _dbContext.MechanicProfiles.Add(new MechanicProfile
                    {
                        UserId = user.Id,
                        Rating = 5.0,
                        TotalJobs = 0,
                        KycStatus = "Incomplete",
                        CommissionRate = 0.20,
                        SkillCategory = "Car",
                        ExperienceYears = 1
                    });
                    await _dbContext.SaveChangesAsync();
                }
            }

            _whatsAppOtpService.ClearPhoneVerification(cleanPhone);
            await SetUserCookies(user);

            string redirectUrl = user.Role == "Customer" ? "/Customer/Dashboard" 
                               : user.Role == "Mechanic" ? "/Mechanic/Dashboard" 
                               : "/Admin/Dashboard";

            return Json(new { success = true, redirect = redirectUrl });
        }

        [HttpPost]
        public async Task<IActionResult> SendOtpForForgotPassword(string phoneNumber, string role)
        {
            string cleanPhone = CleanPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                return Json(new { success = false, message = "Please enter a valid 10-digit mobile number." });
            }

            RaahSathi.Models.User? user;
            if (role == "Admin")
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == "Admin");
                if (user == null)
                {
                    return Json(new { success = false, message = "Access Denied: This mobile number is not registered as an Administrator." });
                }
            }
            else
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && (string.IsNullOrEmpty(role) || u.Role == role));
                if (user == null)
                {
                    user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone));
                }

                if (user == null)
                {
                    return Json(new { success = false, message = "Mobile number is not registered in the system." });
                }
            }

            bool allowEmergency = (role == "Admin" && user != null && user.Role == "Admin");
            var otpResult = await _whatsAppOtpService.SendOtpAsync(cleanPhone, allowEmergency ? "AdminForgotPassword" : "ForgotPassword", allowEmergencyFallback: allowEmergency);
            return Json(new { success = otpResult.Success, message = otpResult.Message, devOtp = allowEmergency ? otpResult.DevOtp : null });
        }

        [HttpPost]
        public IActionResult VerifyOtpForForgotPassword(string phoneNumber, string otp)
        {
            string cleanPhone = CleanPhoneNumber(phoneNumber);
            if (string.IsNullOrEmpty(cleanPhone) || cleanPhone.Length < 10)
            {
                return Json(new { success = false, message = "Please enter a valid 10-digit mobile number." });
            }

            var verifyResult = _whatsAppOtpService.VerifyOtp(cleanPhone, otp);
            return Json(new { success = verifyResult.Success, message = verifyResult.Message });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string phoneNumber, string role, string password)
        {
            string cleanPhone = CleanPhoneNumber(phoneNumber);
            password = password.Trim();

            if (!_whatsAppOtpService.IsPhoneVerified(cleanPhone))
            {
                return Json(new { success = false, message = "WhatsApp number verification is required. Please verify your OTP first." });
            }

            RaahSathi.Models.User? user;
            if (role == "Admin")
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && u.Role == "Admin");
                if (user == null)
                {
                    return Json(new { success = false, message = "Access Denied: Not an authorized Administrator account." });
                }
            }
            else
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => (u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone)) && (string.IsNullOrEmpty(role) || u.Role == role));
                if (user == null)
                {
                    user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == cleanPhone || u.PhoneNumber.EndsWith(cleanPhone));
                }

                if (user == null)
                {
                    return Json(new { success = false, message = "User not found." });
                }
            }


            user.Password = PasswordHasher.HashPassword(password);
            await _dbContext.SaveChangesAsync();

            _whatsAppOtpService.ClearPhoneVerification(cleanPhone);

            // Auto-login after password reset
            await SetUserCookies(user);

            string redirectUrl = user.Role == "Customer" ? "/Customer/Dashboard" 
                               : user.Role == "Mechanic" ? "/Mechanic/Dashboard" 
                               : "/Admin/Dashboard";

            return Json(new { success = true, redirect = redirectUrl });
        }

        public async Task<IActionResult> Logout(string? role)

        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (string.IsNullOrEmpty(role) || role == "Customer") Response.Cookies.Delete("RaahSathiCustomerUserId");
            if (string.IsNullOrEmpty(role) || role == "Mechanic") Response.Cookies.Delete("RaahSathiMechanicUserId");
            if (string.IsNullOrEmpty(role) || role == "Admin") Response.Cookies.Delete("RaahSathiAdminUserId");

            if (string.IsNullOrEmpty(role))
            {
                Response.Cookies.Delete("RaahSathiUserRole");
                Response.Cookies.Delete("RaahSathiUserId");
                Response.Cookies.Delete("RaahSathiUserName");
            }
            return RedirectToAction("Login", "Auth");
        }
    }
}
