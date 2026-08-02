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
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IWebHostEnvironment _env;

        public AuthController(ApplicationDbContext dbContext, IWebHostEnvironment env)
        {
            _dbContext = dbContext;
            _env = env;
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
            var options = new CookieOptions { Expires = DateTime.UtcNow.AddDays(30), HttpOnly = true, IsEssential = true, SameSite = SameSiteMode.Lax, Secure = true };
            Response.Cookies.Append("RaahSathiUserRole", user.Role, options);
            Response.Cookies.Append("RaahSathiUserId", user.Id.ToString(), options);
            Response.Cookies.Append("RaahSathiUserName", user.Name, new CookieOptions { Expires = DateTime.UtcNow.AddDays(30), HttpOnly = false, IsEssential = true, SameSite = SameSiteMode.Lax });

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
        public IActionResult Login(string? role, string? switchRole)
        {
            string? targetRole = role ?? switchRole;

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
            return View();
        }

        [HttpGet]
        [Route("AdminRaahiSathiLogin")]
        [Route("AdminRahiSarhiLogin")]
        [Route("AdminRahiSathiLogin")]
        [Route("AdminRaahSathiLogin")]
        public IActionResult AdminRahiSarhiLogin()
        {
            if (User.Identity?.IsAuthenticated == true && User.IsInRole("Admin"))
            {
                return RedirectToAction("Dashboard", "Admin");
            }
            else
            {
                Response.Cookies.Delete("RaahSathiCustomerUserId");
                Response.Cookies.Delete("RaahSathiMechanicUserId");
                Response.Cookies.Delete("RaahSathiAdminUserId");
                Response.Cookies.Delete("RaahSathiUserRole");
                Response.Cookies.Delete("RaahSathiUserId");
                Response.Cookies.Delete("RaahSathiUserName");
            }

            ViewBag.TargetRole = "Admin";
            return View("Login");
        }

        [HttpPost]
        public async Task<IActionResult> SendOtp(string phoneNumber, string role, string? name)
        {
            // Simple validation
            if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 10)
            {
                return Json(new { success = false, message = "Invalid phone number." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.Role == role);
            
            if (user == null)
            {
                // New user registration flow
                if (string.IsNullOrEmpty(name))
                {
                    // Prompt UI to ask for name
                    return Json(new { success = true, isNewUser = true });
                }

                user = new User
                {
                    Name = name,
                    PhoneNumber = phoneNumber,
                    Role = role,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                // If mechanic, create empty profile
                if (role == "Mechanic")
                {
                    _dbContext.MechanicProfiles.Add(new MechanicProfile
                    {
                        UserId = user.Id,
                        Rating = 5.0,
                        TotalJobs = 0,
                        KycStatus = "Incomplete"
                    });
                    await _dbContext.SaveChangesAsync();
                }
            }

            // In real app, send SMS here. We simulate OTP "1234"
            return Json(new { success = true, isNewUser = false, message = "OTP sent to " + phoneNumber });
        }

        [HttpPost]
        public async Task<IActionResult> VerifyOtp(string phoneNumber, string role, string otp)
        {
            // Dummy OTP restriction for non-development environments
            if (!_env.IsDevelopment() && otp == "1234")
            {
                return Json(new { success = false, message = "OTP verification is restricted to development environments." });
            }

            if (otp != "1234")
            {
                return Json(new { success = false, message = "Invalid OTP. Use 1234 for testing." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.Role == role);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

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

            phoneNumber = phoneNumber.Trim();
            password = password.Trim();

            // Find user by phone number and verify password
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

            if (user == null || (!PasswordHasher.VerifyPassword(password, user.Password) && user.Password != password))
            {
                return Json(new { success = false, message = "Invalid mobile number or password." });
            }

            // Allow login as Admin automatically if user is Admin, or if role matches
            if (user.Role == "Admin" || string.IsNullOrEmpty(role) || user.Role == role)
            {
                await SetUserCookies(user);

                string redirectUrl = user.Role == "Customer" ? "/Customer/Dashboard" 
                                   : user.Role == "Mechanic" ? "/Mechanic/Dashboard" 
                                   : "/Admin/Dashboard";

                return Json(new { success = true, redirect = redirectUrl });
            }

            return Json(new { success = false, message = $"This account is registered as a {user.Role}. Please select the {user.Role} option to log in." });
        }

        [HttpPost]
        public async Task<IActionResult> SendOtpForRegistration(string phoneNumber, string role, string name)
        {
            if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 10)
            {
                return Json(new { success = false, message = "Invalid phone number." });
            }
            if (string.IsNullOrEmpty(name))
            {
                return Json(new { success = false, message = "Please enter your name." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.Role == role);
            if (user != null)
            {
                return Json(new { success = false, message = "Mobile number already registered under this role. Please login." });
            }

            // Simulate OTP
            return Json(new { success = true, message = "OTP sent to " + phoneNumber });
        }

        [HttpPost]
        public async Task<IActionResult> CompleteRegistration(string name, string phoneNumber, string role, string password)
        {
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.Role == role);
            if (user != null)
            {
                return Json(new { success = false, message = "Mobile number already registered. Please login." });
            }

            user = new User
            {
                Name = name,
                PhoneNumber = phoneNumber,
                Role = role,
                Password = PasswordHasher.HashPassword(password),
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

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

            await SetUserCookies(user);

            string redirectUrl = user.Role == "Customer" ? "/Customer/Dashboard" 
                               : user.Role == "Mechanic" ? "/Mechanic/Dashboard" 
                               : "/Admin/Dashboard";

            return Json(new { success = true, redirect = redirectUrl });
        }

        [HttpPost]
        public async Task<IActionResult> SendOtpForForgotPassword(string phoneNumber, string role)
        {
            if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 10)
            {
                return Json(new { success = false, message = "Invalid phone number." });
            }

            phoneNumber = phoneNumber.Trim();

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && (string.IsNullOrEmpty(role) || u.Role == role));
            if (user == null)
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            }

            if (user == null)
            {
                return Json(new { success = false, message = "Mobile number is not registered in the system." });
            }

            return Json(new { success = true, message = "OTP sent to " + phoneNumber });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(string phoneNumber, string role, string password)
        {
            phoneNumber = phoneNumber.Trim();
            password = password.Trim();

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && (string.IsNullOrEmpty(role) || u.Role == role));
            if (user == null)
            {
                user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
            }

            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            user.Password = PasswordHasher.HashPassword(password);
            await _dbContext.SaveChangesAsync();

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
