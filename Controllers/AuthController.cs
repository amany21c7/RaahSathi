using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace RaahSathi.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public AuthController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private void SetUserCookies(User user)
        {
            var options = new CookieOptions { Expires = DateTime.UtcNow.AddDays(30), HttpOnly = false, IsEssential = true, SameSite = SameSiteMode.Lax };
            Response.Cookies.Append("RaahSathiUserRole", user.Role, options);
            Response.Cookies.Append("RaahSathiUserId", user.Id.ToString(), options);
            Response.Cookies.Append("RaahSathiUserName", user.Name, options);

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

            // Check if user is ALREADY logged in via persistent session cookies
            string? mechId = Request.Cookies["RaahSathiMechanicUserId"];
            string? custId = Request.Cookies["RaahSathiCustomerUserId"];
            string? adminId = Request.Cookies["RaahSathiAdminUserId"];
            string? activeRole = Request.Cookies["RaahSathiUserRole"];
            string? activeUserId = Request.Cookies["RaahSathiUserId"];

            // If mechanic is already logged in, redirect directly to Mechanic Dashboard
            if (!string.IsNullOrEmpty(mechId) || activeRole == "Mechanic")
            {
                return RedirectToAction("Dashboard", "Mechanic");
            }
            if (!string.IsNullOrEmpty(custId) || activeRole == "Customer")
            {
                return RedirectToAction("Dashboard", "Customer");
            }
            if (!string.IsNullOrEmpty(adminId) || activeRole == "Admin")
            {
                return RedirectToAction("Dashboard", "Admin");
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
            string? adminId = Request.Cookies["RaahSathiAdminUserId"];
            string? activeRole = Request.Cookies["RaahSathiUserRole"];
            if (!string.IsNullOrEmpty(adminId) || activeRole == "Admin")
            {
                return RedirectToAction("Dashboard", "Admin");
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
            if (otp != "1234")
            {
                return Json(new { success = false, message = "Invalid OTP. Use 1234 for testing." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.Role == role);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            SetUserCookies(user);

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

            // Find user by phone number & password
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber && u.Password == password);

            if (user == null)
            {
                return Json(new { success = false, message = "Invalid mobile number or password." });
            }

            // Allow login as Admin automatically if user is Admin, or if role matches
            if (user.Role == "Admin" || string.IsNullOrEmpty(role) || user.Role == role)
            {
                SetUserCookies(user);

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
                Password = password,
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

            SetUserCookies(user);

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

            user.Password = password;
            await _dbContext.SaveChangesAsync();

            // Auto-login after password reset
            SetUserCookies(user);

            string redirectUrl = user.Role == "Customer" ? "/Customer/Dashboard" 
                               : user.Role == "Mechanic" ? "/Mechanic/Dashboard" 
                               : "/Admin/Dashboard";

            return Json(new { success = true, redirect = redirectUrl });
        }

        public IActionResult Logout(string? role)
        {
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
