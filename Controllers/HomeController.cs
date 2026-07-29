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

            // Dynamic SEO Meta info
            ViewData["Title"] = "RaahSathi | 24x7 Roadside Assistance & Towing Network India";
            ViewData["MetaDescription"] = "RaahSathi is India's leading 24x7 on-demand roadside assistance network. Instantly calculate towing costs and hire verified mechanics for puncture repair, battery jumpstart, and fuel delivery near you.";
            ViewData["MetaKeywords"] = "roadside assistance India, highway mechanic helper, flat tyre repair, towing service Noida, battery jumpstart creta, emergency fuel dispatch, RaahSathi";

            var carBase = pricingRules.FirstOrDefault(r => r.VehicleCategory == "Car")?.BaseFee ?? 99;
            var bikeBase = pricingRules.FirstOrDefault(r => r.VehicleCategory == "2-Wheeler")?.BaseFee ?? 49;
            var host = Request.Host.Value;
            var scheme = Request.Scheme;

            ViewData["StructuredData"] = $@"<script type=""application/ld+json"">
{{
  ""@context"": ""https://schema.org"",
  ""@type"": ""AutoRepair"",
  ""name"": ""RaahSathi Roadside Assistance"",
  ""image"": ""{scheme}://{host}/images/header-logo.png"",
  ""telephone"": ""1800-102-7224"",
  ""priceRange"": ""₹{bikeBase} - ₹{carBase}"",
  ""description"": ""Connected roadside assistance network in India offering transparent upfront pricing and 25-minute ETA."",
  ""address"": {{
    ""@type"": ""PostalAddress"",
    ""addressLocality"": ""Noida"",
    ""addressRegion"": ""Uttar Pradesh"",
    ""postalCode"": ""201301"",
    ""addressCountry"": ""IN""
  }},
  ""geo"": {{
    ""@type"": ""GeoCoordinates"",
    ""latitude"": 28.6273,
    ""longitude"": 77.3725
  }},
  ""openingHoursSpecification"": {{
    ""@type"": ""OpeningHoursSpecification"",
    ""dayOfWeek"": [""Monday"", ""Tuesday"", ""Wednesday"", ""Thursday"", ""Friday"", ""Saturday"", ""Sunday""],
    ""opens"": ""00:00"",
    ""closes"": ""23:59""
  }}
}}
</script>";

            return View();
        }

        public IActionResult HowItWorks()
        {
            ViewData["Title"] = "How RaahSathi Works - Roadside Assistance in 6 Steps";
            ViewData["MetaDescription"] = "Learn how RaahSathi's on-demand roadside assistance works. Follow our simple 6-step dispatch workflow from upfront cost estimation to live GPS mechanic tracking and secure escrow payouts.";
            ViewData["MetaKeywords"] = "how roadside assistance works, towing dispatch process, escrow auto repair payments, live GPS mechanic tracking, RaahSathi process";
            return View();
        }

        public async Task<IActionResult> Services()
        {
            ViewData["Title"] = "Services - One platform, every vehicle, every breakdown";
            var pricingRules = await _dbContext.PricingRules.ToListAsync();
            ViewBag.PricingRules = pricingRules;

            ViewData["MetaDescription"] = "Explore RaahSathi's emergency breakdown services. Get upfront prices and prompt dispatch for battery jumpstarts, flat tyre repair, towing, lockouts, fuel delivery, and mechanical checkups.";
            ViewData["MetaKeywords"] = "battery jumpstart service, flat tyre repair near me, emergency towing services, car lockout help, flatbed towing, highway fuel delivery";
            return View();
        }

        public IActionResult AboutUs()
        {
            ViewData["Title"] = "About Us - On Every Road, A Trusted Companion";
            ViewData["MetaDescription"] = "Meet RaahSathi, India's trusted roadside companion. Read our mission to eliminate verbal price negotiation, background-verify all mechanics, and build a premium digital emergency network.";
            ViewData["MetaKeywords"] = "about RaahSathi, roadside assistance mission, verified auto mechanics network, transparent towing prices, Aman yadav RaahSathi";
            return View();
        }

        public IActionResult ContactUs()
        {
            ViewData["Title"] = "Contact Us - 24x7 Emergency Roadside Support & Partnership";
            ViewData["MetaDescription"] = "Contact RaahSathi support desk. Reach our 24x7 toll-free emergency highway hotline 1800-102-7224, submit partnership inquiries, or send us feedback for instant resolution.";
            ViewData["MetaKeywords"] = "RaahSathi contact number, roadside assistance helpline, toll free highway number, support email, mechanic partnership contact";
            return View();
        }

        public IActionResult Manual()
        {
            ViewData["Title"] = "Testing Manual & System Documentation - RaahSathi";
            ViewData["MetaDescription"] = "RaahSathi Testing Manual and System Integration Documentation for operators, mechanics, and administrators.";
            ViewData["MetaKeywords"] = "RaahSathi manual, operator handbook, system integration documentation";
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
            ViewData["MetaDescription"] = "Read the RaahSathi Privacy Policy. Learn how we safeguard user data, handle GPS location history, verify profiles, and enforce security mechanisms across our roadside network.";
            ViewData["MetaKeywords"] = "privacy policy RaahSathi, data safety, GPS tracking privacy, security protocol, mechanic background checks";
            return View();
        }

        public IActionResult Terms()
        {
            ViewData["Title"] = "Terms & Conditions - RaahSathi Platform Rules";
            ViewData["MetaDescription"] = "View the Terms and Conditions of using the RaahSathi platform. Understand customer obligations, technician service rules, legal liabilities, and agreement details.";
            ViewData["MetaKeywords"] = "terms and conditions, user agreement, roadside assistance terms, legal rules, platform compliance, liability clause";
            return View();
        }

        public IActionResult RefundPolicy()
        {
            ViewData["Title"] = "Refund & Escrow Guarantee Policy - RaahSathi";
            ViewData["MetaDescription"] = "Review RaahSathi's Refund & Escrow Guarantee Policy. Details on how payments are held in escrow and released only upon verified completion of roadside breakdown service.";
            ViewData["MetaKeywords"] = "refund policy, escrow payment guarantee, money back breakdown service, payment refund, verified payment release";
            return View();
        }

        public IActionResult CancellationPolicy()
        {
            ViewData["Title"] = "Cancellation & ETA Service Level Policy - RaahSathi";
            ViewData["MetaDescription"] = "Understand RaahSathi's Cancellation Policy and ETA Service Level Agreements. Rules for cancellations by customers or mechanics, and dispatch timings SLA.";
            ViewData["MetaKeywords"] = "cancellation policy, ETA roadside assistance, booking cancellation, technician arrival SLA, cancellation fees";
            return View();
        }

        public IActionResult Faq()
        {
            ViewData["Title"] = "Frequently Asked Questions (FAQs) - RaahSathi";
            ViewData["MetaDescription"] = "Find answers to FAQs about RaahSathi. Details on booking mechanics, estimating upfront costs, platform commission fee structures, and resolving disputes.";
            ViewData["MetaKeywords"] = "roadside assistance FAQs, towing service costs, mechanic tracker help, escrow payments faq, weather surge Noida";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AskAiChat(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return Json(new { success = false, answer = "Please type a valid query." });
            }

            // Fetch live PricingRules & ProblemTypePricings from Database dynamically so Admin Console updates reflect immediately
            List<PricingRule> rules;
            List<ProblemTypePricing> problemTypesList;
            try
            {
                rules = await _dbContext.PricingRules.ToListAsync();
                problemTypesList = await _dbContext.ProblemTypePricings.Where(p => p.IsActive).ToListAsync();
            }
            catch
            {
                rules = new List<PricingRule>();
                problemTypesList = new List<ProblemTypePricing>();
            }

            var carRule = rules.FirstOrDefault(r => r.VehicleCategory == "Car") ?? new PricingRule { BaseFee = 99, PerKmRate = 8 };
            var bikeRule = rules.FirstOrDefault(r => r.VehicleCategory == "2-Wheeler") ?? new PricingRule { BaseFee = 49, PerKmRate = 5 };
            var commRule = rules.FirstOrDefault(r => r.VehicleCategory == "Commercial") ?? new PricingRule { BaseFee = 199, PerKmRate = 12 };
            var heavyRule = rules.FirstOrDefault(r => r.VehicleCategory == "Heavy") ?? new PricingRule { BaseFee = 299, PerKmRate = 15 };

            string problemPricesSummary = string.Join("; ", problemTypesList.Select(pt => $"{pt.ProblemName} ({pt.VehicleCategory}): ₹{pt.MinServiceCharge}-₹{pt.MaxServiceCharge}"));
            if (string.IsNullOrWhiteSpace(problemPricesSummary))
            {
                problemPricesSummary = "Battery ₹150-3500, Tyre ₹200-800, Fuel Delivery ₹250+, Lockout ₹200-600, Brake/Clutch ₹200-1000";
            }

            string apiKey = _configuration["GroqApiKey"] ?? Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? "";

            string systemPrompt = $@"You are RaahSathi AI Support Assistant, India's 24x7 connected emergency roadside assistance AI.
RaahSathi Services & Information (Updated Live from Database):
- Platform: RaahSathi connects stranded drivers with verified patrol mechanics across 20+ cities & highways in India.
- Emergency 24x7 Hotline: 1800-102-7224 (Toll-Free).
- Upfront 2-Layer Pricing System (LIVE ADMIN RATES):
  * Layer 1 (Visiting Charge): Base Fee + (2 * Distance * Rate per KM) (to cover the mechanic's round trip).
    - Car/SUV/Van: Base ₹{carRule.BaseFee} + 2 * ₹{carRule.PerKmRate}/km
    - 2-Wheeler/Auto: Base ₹{bikeRule.BaseFee} + 2 * ₹{bikeRule.PerKmRate}/km
    - Commercial Truck/Pickup: Base ₹{commRule.BaseFee} + 2 * ₹{commRule.PerKmRate}/km
    - Heavy JCB/Crane: Base ₹{heavyRule.BaseFee} + 2 * ₹{heavyRule.PerKmRate}/km
  * Layer 2 (Service Fix Fee Overrides): {problemPricesSummary}.
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

        [Route("sitemap.xml")]
        public IActionResult Sitemap()
        {
            var host = Request.Host.Value;
            var scheme = Request.Scheme;
            var baseUrl = $"{scheme}://{host}";
            
            var sitemapContent = new System.Text.StringBuilder();
            sitemapContent.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sitemapContent.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            void AddUrl(string path, string frequency, string priority)
            {
                sitemapContent.AppendLine("  <url>");
                sitemapContent.AppendLine($"    <loc>{baseUrl}{path}</loc>");
                sitemapContent.AppendLine($"    <lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod>");
                sitemapContent.AppendLine($"    <changefreq>{frequency}</changefreq>");
                sitemapContent.AppendLine($"    <priority>{priority}</priority>");
                sitemapContent.AppendLine("  </url>");
            }

            // Public Routes
            AddUrl("/", "daily", "1.0");
            AddUrl("/Home/Services", "weekly", "0.9");
            AddUrl("/Home/HowItWorks", "weekly", "0.8");
            AddUrl("/Home/AboutUs", "monthly", "0.7");
            AddUrl("/Home/ContactUs", "monthly", "0.7");
            AddUrl("/Home/Faq", "weekly", "0.6");
            AddUrl("/Home/Privacy", "yearly", "0.5");
            AddUrl("/Home/Terms", "yearly", "0.5");
            AddUrl("/Home/RefundPolicy", "yearly", "0.5");
            AddUrl("/Home/CancellationPolicy", "yearly", "0.5");

            sitemapContent.AppendLine("</urlset>");
            return Content(sitemapContent.ToString(), "application/xml", System.Text.Encoding.UTF8);
        }

        [Route("robots.txt")]
        public IActionResult RobotsText()
        {
            var host = Request.Host.Value;
            var scheme = Request.Scheme;
            var robots = new System.Text.StringBuilder();
            robots.AppendLine("User-agent: *");
            robots.AppendLine("Allow: /");
            robots.AppendLine("Disallow: /Admin/");
            robots.AppendLine("Disallow: /Customer/");
            robots.AppendLine("Disallow: /Mechanic/");
            robots.AppendLine("Disallow: /Auth/");
            robots.AppendLine($"Sitemap: {scheme}://{host}/sitemap.xml");

            return Content(robots.ToString(), "text/plain", System.Text.Encoding.UTF8);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
