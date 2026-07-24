using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaahSathi.Data;
using RaahSathi.Models;
using RaahSathi.Services;

namespace RaahSathi.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IPricingEngine _pricingEngine;
        private readonly IConfiguration _configuration;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext, IPricingEngine pricingEngine, IConfiguration configuration)
        {
            _logger = logger;
            _dbContext = dbContext;
            _pricingEngine = pricingEngine;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            // Seed base pricing stats for the upfront calculator
            var pricingRules = await _dbContext.PricingRules.ToListAsync();
            ViewBag.PricingRules = pricingRules;
            ViewBag.ProblemTypes = await _dbContext.ProblemTypePricings.Where(p => p.IsActive).OrderBy(p => p.VehicleCategory).ThenBy(p => p.ProblemName).ToListAsync();
            return View();
        }

        public IActionResult HowItWorks()
        {
            ViewData["Title"] = "How RaahSathi Works - Roadside Assistance in 6 Steps";
            return View();
        }

        public async Task<IActionResult> Services()
        {
            ViewData["Title"] = "Services - One platform, every vehicle, every breakdown";
            var pricingRules = await _dbContext.PricingRules.ToListAsync();
            ViewBag.PricingRules = pricingRules;
            return View();
        }

        public IActionResult AboutUs()
        {
            ViewData["Title"] = "About Us - On Every Road, A Trusted Companion";
            return View();
        }

        public IActionResult ContactUs()
        {
            ViewData["Title"] = "Contact Us - 24x7 Emergency Roadside Support & Partnership";
            return View();
        }

        public IActionResult Manual()
        {
            ViewData["Title"] = "Testing Manual & System Documentation - RaahSathi";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SubmitContactForm(string fullName, string phone, string email, string subject, string message)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, message = "Please fill in all required fields." });
            }

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

            var contactMsg = new ContactMessage
            {
                FullName = fullName.Trim(),
                Phone = phone.Trim(),
                Email = email?.Trim() ?? "",
                Subject = subject?.Trim() ?? "General Inquiry",
                Message = message.Trim(),
                CreatedAt = DateTime.UtcNow,
                Status = "Pending"
            };

            _dbContext.ContactMessages.Add(contactMsg);
            await _dbContext.SaveChangesAsync();

            return Json(new { success = true, message = "Your message has been received! Our support desk will reach out within 15 minutes." });
        }

        [HttpGet]
        public IActionResult SwitchIdentity(string role, int userId, string name)
        {
            // Set cookies for simulation identity
            var options = new CookieOptions
            {
                Expires = DateTime.UtcNow.AddDays(7),
                HttpOnly = false, // Allow client-side JS read for real-time triggers
                IsEssential = true
            };

            Response.Cookies.Append("RahiUserRole", role, options);
            Response.Cookies.Append("RahiUserId", userId.ToString(), options);
            Response.Cookies.Append("RahiUserName", Uri.UnescapeDataString(name), options);

            // Redirect to appropriate portal dashboard
            return role switch
            {
                "Customer" => RedirectToAction("Dashboard", "Customer"),
                "Mechanic" => RedirectToAction("Dashboard", "Mechanic"),
                "Admin" => RedirectToAction("Dashboard", "Admin"),
                _ => RedirectToAction("Index")
            };
        }

        [HttpGet]
        public IActionResult ClearIdentity()
        {
            Response.Cookies.Delete("RahiUserRole");
            Response.Cookies.Delete("RahiUserId");
            Response.Cookies.Delete("RahiUserName");
            return RedirectToAction("Index");
        }

        public IActionResult Privacy()
        {
            ViewData["Title"] = "Privacy Policy - RaahSathi Data & Safety Rules";
            return View();
        }

        public IActionResult Faq()
        {
            ViewData["Title"] = "Frequently Asked Questions (FAQs) - RaahSathi";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AskAiChat(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return Json(new { success = false, answer = "Please type a valid query." });
            }

            // Fetch live PricingRules from Database dynamically so Admin Console updates reflect immediately
            List<PricingRule> rules;
            try
            {
                rules = await _dbContext.PricingRules.ToListAsync();
            }
            catch
            {
                rules = new List<PricingRule>();
            }

            var carRule = rules.FirstOrDefault(r => r.VehicleCategory == "Car") ?? new PricingRule { BaseFee = 99, PerKmRate = 8 };
            var bikeRule = rules.FirstOrDefault(r => r.VehicleCategory == "2-Wheeler") ?? new PricingRule { BaseFee = 49, PerKmRate = 5 };
            var commRule = rules.FirstOrDefault(r => r.VehicleCategory == "Commercial") ?? new PricingRule { BaseFee = 199, PerKmRate = 12 };
            var heavyRule = rules.FirstOrDefault(r => r.VehicleCategory == "Heavy") ?? new PricingRule { BaseFee = 299, PerKmRate = 15 };

            string apiKey = _configuration["GroqApiKey"] ?? Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";

            string systemPrompt = $@"You are RaahSathi AI Support Assistant, India's 24x7 connected emergency roadside assistance AI.
RaahSathi Services & Information (Updated Live from Database):
- Platform: RaahSathi connects stranded drivers with verified patrol mechanics across 20+ cities & highways in India.
- Emergency 24x7 Hotline: 1800-102-7224 (Toll-Free).
- Upfront 2-Layer Pricing System (LIVE ADMIN RATES):
  * Layer 1 (Visiting Charge): Base Fee + (Distance * Rate per KM).
    - Car/SUV/Van: Base ₹{carRule.BaseFee} + ₹{carRule.PerKmRate}/km
    - 2-Wheeler/Auto: Base ₹{bikeRule.BaseFee} + ₹{bikeRule.PerKmRate}/km
    - Commercial Truck/Pickup: Base ₹{commRule.BaseFee} + ₹{commRule.PerKmRate}/km
    - Heavy JCB/Crane: Base ₹{heavyRule.BaseFee} + ₹{heavyRule.PerKmRate}/km
  * Layer 2 (Service Fix Fee): Standard repair fix range (e.g. Battery ₹150-3500, Tyre ₹200-800, Fuel ₹250+fuel, Towing ₹300-1500, Engine Check ₹180-1200).
- Zero Verbal Negotiation & Escrow Lock Security.
- 100% KYC Verified Mechanics with live GPS tracking on driver map.
Answer queries concisely, politely, and accurately in English or Hinglish.";

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    using var client = new System.Net.Http.HttpClient();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    var reqBody = new
                    {
                        model = "llama-3.3-70b-versatile",
                        messages = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = prompt }
                        },
                        max_tokens = 300,
                        temperature = 0.6
                    };

                    var jsonContent = new System.Net.Http.StringContent(
                        System.Text.Json.JsonSerializer.Serialize(reqBody),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", jsonContent);

                    if (response.IsSuccessStatusCode)
                    {
                        var respStr = await response.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(respStr);
                        string aiAnswer = doc.RootElement
                            .GetProperty("choices")[0]
                            .GetProperty("message")
                            .GetProperty("content")
                            .GetString() ?? "";

                        if (!string.IsNullOrWhiteSpace(aiAnswer))
                        {
                            return Json(new { success = true, answer = aiAnswer.Trim() });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error calling Groq API");
                }
            }

            // Built-in intelligent fallback knowledge engine trained on live DB pricing rules
            string lower = prompt.ToLower();
            string answer = "I am RaahSathi's 24x7 AI Support Assistant! For instant emergency breakdown assistance, call our Toll-Free Helpline at 1800-102-7224.";

            if (lower.Contains("book") || lower.Contains("request") || lower.Contains("mechanic") || lower.Contains("hire"))
            {
                answer = "To request a mechanic, use the Upfront Cost Estimator on our Home page to choose your vehicle type and distance, then click 'Request Assistance Now'. Verified mechanics nearby will be dispatched immediately!";
            }
            else if (lower.Contains("charge") || lower.Contains("price") || lower.Contains("cost") || lower.Contains("visiting") || lower.Contains("rate") || lower.Contains("fee"))
            {
                answer = $"RaahSathi Live Pricing Rules (Updated by Admin):\n• Car/SUV: Base ₹{carRule.BaseFee} + ₹{carRule.PerKmRate}/km\n• 2-Wheeler: Base ₹{bikeRule.BaseFee} + ₹{bikeRule.PerKmRate}/km\n• Commercial: Base ₹{commRule.BaseFee} + ₹{commRule.PerKmRate}/km\n• Heavy Vehicle: Base ₹{heavyRule.BaseFee} + ₹{heavyRule.PerKmRate}/km\nZero verbal negotiation & 100% transparent upfront calculations!";
            }
            else if (lower.Contains("number") || lower.Contains("helpline") || lower.Contains("hotline") || lower.Contains("phone") || lower.Contains("call") || lower.Contains("contact"))
            {
                answer = "Our 24x7 Emergency Highway Hotline is 1800-102-7224 (Toll-Free). You can also click the red SOS button in the footer for instant dispatch!";
            }
            else if (lower.Contains("track") || lower.Contains("gps") || lower.Contains("location"))
            {
                answer = "Once a mechanic accepts your job, you receive live GPS tracking on your map with SMS alerts so you and your family know exact arrival time.";
            }
            else if (lower.Contains("tyre") || lower.Contains("battery") || lower.Contains("fuel") || lower.Contains("towing") || lower.Contains("engine"))
            {
                answer = "RaahSathi provides 5 core emergency services: Battery Jump-start (₹150-₹3,500), Flat Tyre Repair (₹200-₹800), Emergency Fuel Delivery (₹250+fuel), Towing & Flatbed (₹300-₹1,500), and Engine/Mechanical Checkup (₹180-₹1,200).";
            }
            else if (lower.Contains("join") || lower.Contains("partner") || lower.Contains("register mechanic") || lower.Contains("workshop"))
            {
                answer = "Mechanics & Workshops can join RaahSathi by completing 100% KYC Audit at /Mechanic/KycForm. Enjoy instant job alerts, direct wallet payouts, and fair working rules!";
            }

            return Json(new { success = true, answer = answer });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
