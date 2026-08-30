using System.Diagnostics;
using System.Net;
using System.Net.Mail;
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

        private async Task PopulateGlobalSeoDataAsync()
        {
            try
            {
                var settings = await _dbContext.AdminSystemSettings
                    .Where(s => s.SettingKey == "GoogleSiteVerificationTag" || s.SettingKey == "GoogleAnalyticsId" || s.SettingKey == "DefaultMetaKeywords")
                    .ToListAsync();
                
                var googleVer = settings.FirstOrDefault(s => s.SettingKey == "GoogleSiteVerificationTag")?.SettingValue;
                var googleGa = settings.FirstOrDefault(s => s.SettingKey == "GoogleAnalyticsId")?.SettingValue;

                if (!string.IsNullOrEmpty(googleVer)) ViewData["GoogleSiteVerification"] = googleVer;
                if (!string.IsNullOrEmpty(googleGa)) ViewData["GoogleAnalyticsId"] = googleGa;
            }
            catch
            {
                // Graceful fallback
            }
        }

        public async Task<IActionResult> Index()
        {
            await PopulateGlobalSeoDataAsync();

            // Seed base pricing stats for the upfront calculator
            var pricingRules = await _dbContext.PricingRules.ToListAsync();
            ViewBag.PricingRules = pricingRules;
            ViewBag.ProblemTypes = await _dbContext.ProblemTypePricings.Where(p => p.IsActive).OrderBy(p => p.VehicleCategory).ThenBy(p => p.ProblemName).ToListAsync();

            // Dynamic SEO Meta info
            ViewData["Title"] = "RaahSathi | 24x7 Roadside Assistance & Towing Network India";
            ViewData["MetaDescription"] = "RaahSathi is India's leading 24x7 on-demand roadside assistance network. Instantly calculate towing costs and hire verified mechanics for puncture repair, battery jumpstart, and fuel delivery near you.";
            ViewData["MetaKeywords"] = "roadside assistance India, highway mechanic helper, flat tyre repair, towing service Noida, battery jumpstart creta, emergency fuel dispatch, RaahSathi, car breakdown help";

            var carBase = pricingRules.FirstOrDefault(r => r.VehicleCategory == "Car")?.BaseFee ?? 299;
            var bikeBase = pricingRules.FirstOrDefault(r => r.VehicleCategory == "2-Wheeler")?.BaseFee ?? 199;
            var host = Request.Host.Value;
            var scheme = Request.Scheme;

            ViewData["StructuredData"] = $@"<script type=""application/ld+json"">
{{
  ""@context"": ""https://schema.org"",
  ""@type"": ""AutoRepair"",
  ""name"": ""RaahSathi Roadside Assistance"",
  ""image"": ""{scheme}://{host}/images/header-logo.png"",
  ""telephone"": ""9891819236"",
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

        public async Task<IActionResult> HowItWorks()
        {
            await PopulateGlobalSeoDataAsync();
            ViewData["Title"] = "How RaahSathi Works - Roadside Assistance in 6 Steps";
            ViewData["MetaDescription"] = "Learn how RaahSathi's on-demand roadside assistance works. Follow our simple 6-step dispatch workflow from upfront cost estimation to live GPS mechanic tracking and secure escrow payouts.";
            ViewData["MetaKeywords"] = "how roadside assistance works, towing dispatch process, escrow auto repair payments, live GPS mechanic tracking, RaahSathi process";
            return View();
        }

        public async Task<IActionResult> Services(string? city, string? service)
        {
            await PopulateGlobalSeoDataAsync();
            var pricingRules = await _dbContext.PricingRules.ToListAsync();
            ViewBag.PricingRules = pricingRules;

            if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(service))
            {
                ViewData["Title"] = $"24x7 {service} in {city} | Fast 15-Min Mechanic - RaahSathi";
                ViewData["MetaDescription"] = $"Instant {service} and emergency roadside assistance in {city}. Upfront transparent pricing, verified mechanics, live GPS tracking, and prompt 24x7 dispatch.";
                ViewData["MetaKeywords"] = $"{service} {city}, mechanic near me {city}, emergency breakdown {city}, 24x7 towing {city}, RaahSathi {city}";
            }
            else if (!string.IsNullOrWhiteSpace(city))
            {
                ViewData["Title"] = $"24x7 Roadside Assistance in {city} | Mechanics & Towing - RaahSathi";
                ViewData["MetaDescription"] = $"Best 24x7 emergency roadside assistance network in {city}. Verified mechanics for puncture repair, battery jumpstart, towing, and fuel delivery with upfront pricing.";
                ViewData["MetaKeywords"] = $"roadside assistance {city}, mechanic near me {city}, car breakdown service {city}, towing service {city}, emergency mechanic {city}";
            }
            else if (!string.IsNullOrWhiteSpace(service))
            {
                ViewData["Title"] = $"{service} Near Me - 24x7 On-Demand Roadside Assistance | RaahSathi";
                ViewData["MetaDescription"] = $"Find verified mechanics for {service} near you across India. Instant transparent price estimation, 20-min average arrival, and guaranteed service.";
                ViewData["MetaKeywords"] = $"{service} near me, fast {service}, 24x7 {service} helpline, car {service}, bike {service}, RaahSathi";
            }
            else
            {
                ViewData["Title"] = "Services - One platform, every vehicle, every breakdown";
                ViewData["MetaDescription"] = "Explore RaahSathi's emergency breakdown services. Get upfront prices and prompt dispatch for battery jumpstarts, flat tyre repair, towing, lockouts, fuel delivery, and mechanical checkups.";
                ViewData["MetaKeywords"] = "battery jumpstart service, flat tyre repair near me, emergency towing services, car lockout help, flatbed towing, highway fuel delivery";
            }

            ViewBag.SelectedCity = city;
            ViewBag.SelectedService = service;
            return View();
        }

        [HttpGet("Home/AboutUs")]
        [HttpGet("Home/About")]
        [HttpGet("AboutUs")]
        [HttpGet("About")]
        public IActionResult AboutUs()
        {
            ViewData["Title"] = "About Us - On Every Road, A Trusted Companion";
            ViewData["MetaDescription"] = "Meet RaahSathi, India's trusted roadside companion. Read our mission to eliminate verbal price negotiation, background-verify all mechanics, and build a premium digital emergency network.";
            ViewData["MetaKeywords"] = "about RaahSathi, roadside assistance mission, verified auto mechanics network, transparent towing prices, Aman yadav RaahSathi";
            return View();
        }

        [HttpGet("Home/ContactUs")]
        [HttpGet("Home/Contact")]
        [HttpGet("ContactUs")]
        [HttpGet("Contact")]
        public IActionResult ContactUs()
        {
            ViewData["Title"] = "Contact Us - 24x7 Emergency Roadside Support & Partnership";
            ViewData["MetaDescription"] = "Contact RaahSathi support desk. Reach our 24x7 emergency highway helpline +91 9891819236, submit partnership inquiries, or send us feedback for instant resolution.";
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

            // 📧 Send email notification to support.raahsathi@gmail.com
            _ = Task.Run(async () =>
            {
                await SendContactNotificationEmailAsync(contactMsg.FullName, contactMsg.Phone, contactMsg.Email, contactMsg.Subject, contactMsg.Message, "Contact Us Form");
            });

            return Json(new { success = true, message = "Your message has been received! Our support desk will reach out within 15 minutes." });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSupportTicket(string fullName, string email, string phone, string subject, string message, string userRole, IFormFile? photoFile)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, message = "Name, Phone, and Message are required." });
            }

            string photoUrl = "";
            if (photoFile != null && photoFile.Length > 0)
            {
                try
                {
                    string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "support");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(photoFile.FileName);
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await photoFile.CopyToAsync(fileStream);
                    }
                    photoUrl = "/uploads/support/" + uniqueFileName;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Support attachment upload error: " + ex.Message);
                }
            }

            var contactMsg = new ContactMessage
            {
                FullName = fullName.Trim(),
                Phone = phone.Trim(),
                Email = email?.Trim() ?? "",
                Subject = subject?.Trim() ?? "Support Ticket",
                Message = message.Trim(),
                CreatedAt = DateTime.UtcNow,
                Status = "Pending",
                PhotoUrl = photoUrl,
                UserRole = string.IsNullOrWhiteSpace(userRole) ? "Guest" : userRole
            };

            _dbContext.ContactMessages.Add(contactMsg);
            await _dbContext.SaveChangesAsync();

            // 📧 Send email notification to support.raahsathi@gmail.com
            _ = Task.Run(async () =>
            {
                await SendContactNotificationEmailAsync(contactMsg.FullName, contactMsg.Phone, contactMsg.Email, contactMsg.Subject, contactMsg.Message, "Support Ticket Portal");
            });

            return Json(new { success = true, message = "Support ticket submitted successfully! Admin support team will contact you shortly." });
        }

        private async Task SendContactNotificationEmailAsync(string fullName, string phone, string email, string subject, string message, string source = "Contact Us Page")
        {
            try
            {
                using var mail = new MailMessage();
                mail.From = new MailAddress("support.raahsathi@gmail.com", "RaahSathi Desk");
                mail.To.Add("support.raahsathi@gmail.com");

                if (!string.IsNullOrWhiteSpace(email) && email.Contains("@"))
                {
                    try { mail.ReplyToList.Add(new MailAddress(email, fullName)); } catch { }
                }

                mail.Subject = $"[RaahSathi Contact] {subject} - From {fullName}";
                mail.IsBodyHtml = true;
                mail.Body = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #ffffff;'>
                        <div style='background: linear-gradient(135deg, #ff6b00 0%, #d97706 100%); padding: 18px; border-radius: 6px; text-align: center; color: #ffffff;'>
                            <h2 style='margin: 0; font-size: 22px; letter-spacing: 0.5px;'>🚗 RaahSathi - New Contact Inquiry</h2>
                            <p style='margin: 6px 0 0 0; font-size: 14px; opacity: 0.95;'>Source: {source}</p>
                        </div>
                        <div style='padding: 20px 0;'>
                            <table style='width: 100%; border-collapse: collapse; font-size: 14px;'>
                                <tr style='border-bottom: 1px solid #f1f5f9;'>
                                    <td style='padding: 10px 0; font-weight: bold; width: 35%; color: #64748b;'>Sender Name:</td>
                                    <td style='padding: 10px 0; color: #0f172a; font-weight: 600;'>{fullName}</td>
                                </tr>
                                <tr style='border-bottom: 1px solid #f1f5f9;'>
                                    <td style='padding: 10px 0; font-weight: bold; color: #64748b;'>Mobile Number:</td>
                                    <td style='padding: 10px 0; color: #0f172a;'><a href='tel:{phone}' style='color: #ff6b00; text-decoration: none; font-weight: bold;'>{phone}</a></td>
                                </tr>
                                <tr style='border-bottom: 1px solid #f1f5f9;'>
                                    <td style='padding: 10px 0; font-weight: bold; color: #64748b;'>Email Address:</td>
                                    <td style='padding: 10px 0; color: #0f172a;'><a href='mailto:{email}' style='color: #0284c7; text-decoration: none;'>{(string.IsNullOrWhiteSpace(email) ? "Not Provided" : email)}</a></td>
                                </tr>
                                <tr style='border-bottom: 1px solid #f1f5f9;'>
                                    <td style='padding: 10px 0; font-weight: bold; color: #64748b;'>Inquiry Subject:</td>
                                    <td style='padding: 10px 0; color: #0f172a; font-weight: bold;'>{subject}</td>
                                </tr>
                                <tr style='border-bottom: 1px solid #f1f5f9;'>
                                    <td style='padding: 10px 0; font-weight: bold; color: #64748b;'>Submission Time:</td>
                                    <td style='padding: 10px 0; color: #64748b;'>{DateTime.UtcNow.AddHours(5.5):dd MMM yyyy, hh:mm tt} (IST)</td>
                                </tr>
                            </table>
                            <div style='margin-top: 20px; padding: 15px; background-color: #f8fafc; border-left: 4px solid #ff6b00; border-radius: 4px;'>
                                <p style='margin: 0 0 8px 0; font-weight: bold; color: #334155; font-size: 13px; text-transform: uppercase;'>Message Content:</p>
                                <p style='margin: 0; color: #1e293b; white-space: pre-wrap; line-height: 1.6; font-size: 14px;'>{message}</p>
                            </div>
                        </div>
                        <div style='border-top: 1px solid #e2e8f0; padding-top: 15px; text-align: center; color: #94a3b8; font-size: 12px;'>
                            <p style='margin: 0;'>This inquiry is also logged in real-time in the <strong>RaahSathi Admin Dashboard &gt; Messages</strong> portal.</p>
                        </div>
                    </div>";

                using var client = new SmtpClient("smtp.gmail.com", 587);
                client.EnableSsl = true;
                client.UseDefaultCredentials = false;
                
                // If SMTP password or credentials are configured, use them
                var smtpPass = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? _configuration?["Smtp:Password"];
                var smtpUser = Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? _configuration?["Smtp:Username"] ?? "support.raahsathi@gmail.com";
                if (!string.IsNullOrWhiteSpace(smtpPass))
                {
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);
                }

                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                // Non-blocking log so contact message is safely stored in database even if SMTP is offline
                Console.WriteLine($"[Contact Email Notification] Note: {ex.Message}");
            }
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
- Emergency 24x7 Hotline: {ContactInfoHelper.HelplineNumber} (Helpline), WhatsApp: {ContactInfoHelper.WhatsAppNumber}, Email: {ContactInfoHelper.SupportEmail}.
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
                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                    var requestPayload = new
                    {
                        model = "llama3-70b-8192",
                        messages = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = prompt }
                        },
                        temperature = 0.5,
                        max_tokens = 300
                    };

                    var content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestPayload), System.Text.Encoding.UTF8, "application/json");
                    var response = await httpClient.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        using var jsonDoc = System.Text.Json.JsonDocument.Parse(responseString);
                        var root = jsonDoc.RootElement;
                        var aiAnswer = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

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
            string answer = $"I am RaahSathi's 24x7 AI Support Assistant! For instant emergency breakdown assistance, call our 24×7 Helpline at {ContactInfoHelper.HelplineNumber}.";

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
                answer = $"Our 24x7 Emergency Highway Helpline is {ContactInfoHelper.HelplineNumber}. WhatsApp support: {ContactInfoHelper.WhatsAppNumber}. You can also click the red SOS button in the footer for instant dispatch!";
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
        public async Task<IActionResult> Sitemap()
        {
            var host = Request.Host.Value;
            var scheme = Request.Scheme;
            var baseUrl = $"{scheme}://{host}";
            
            var sitemapContent = new System.Text.StringBuilder();
            sitemapContent.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sitemapContent.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">");

            void AddUrl(string path, string frequency, string priority)
            {
                string encodedPath = path.Replace("&", "&amp;");
                sitemapContent.AppendLine("  <url>");
                sitemapContent.AppendLine($"    <loc>{baseUrl}{encodedPath}</loc>");
                sitemapContent.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"en\" href=\"{baseUrl}{encodedPath}\" />");
                sitemapContent.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"hi\" href=\"{baseUrl}{encodedPath}{(path.Contains('?') ? "&amp;" : "?")}lang=hi\" />");
                sitemapContent.AppendLine($"    <xhtml:link rel=\"alternate\" hreflang=\"x-default\" href=\"{baseUrl}{encodedPath}\" />");
                sitemapContent.AppendLine($"    <lastmod>{DateTime.UtcNow:yyyy-MM-dd}</lastmod>");
                sitemapContent.AppendLine($"    <changefreq>{frequency}</changefreq>");
                sitemapContent.AppendLine($"    <priority>{priority}</priority>");
                sitemapContent.AppendLine("  </url>");
            }

            // Core Public Routes
            AddUrl("/", "daily", "1.0");
            AddUrl("/Home/Services", "daily", "0.95");
            AddUrl("/Home/HowItWorks", "weekly", "0.85");
            AddUrl("/Home/AboutUs", "monthly", "0.80");
            AddUrl("/Home/ContactUs", "monthly", "0.80");
            AddUrl("/Home/Faq", "weekly", "0.80");
            AddUrl("/Home/Privacy", "monthly", "0.50");
            AddUrl("/Home/Terms", "monthly", "0.50");
            AddUrl("/Home/RefundPolicy", "monthly", "0.50");
            AddUrl("/Home/CancellationPolicy", "monthly", "0.50");

            // Dynamic Programmatic City & Local Breakdown SEO Routes
            try
            {
                var cities = await _dbContext.CityServiceAreas.Where(c => c.IsActive).Select(c => c.CityName).Distinct().ToListAsync();
                if (!cities.Any())
                {
                    cities = new List<string> { "Noida", "Delhi", "Gurgaon", "Ghaziabad", "Faridabad", "Greater Noida", "Lucknow", "Jaipur", "Agra", "Kanpur" };
                }

                foreach (var city in cities)
                {
                    AddUrl($"/Home/Services?city={Uri.EscapeDataString(city)}", "weekly", "0.90");
                }

                var serviceProblems = await _dbContext.ProblemTypePricings.Where(p => p.IsActive).Select(p => p.ProblemName).Distinct().ToListAsync();
                foreach (var problem in serviceProblems)
                {
                    AddUrl($"/Home/Services?service={Uri.EscapeDataString(problem)}", "weekly", "0.85");
                }

                // Top City + Problem Combinations (Hyper-Local High-Intent Keywords)
                var topProblems = serviceProblems.Take(5).ToList();
                var topCities = cities.Take(5).ToList();
                foreach (var city in topCities)
                {
                    foreach (var prob in topProblems)
                    {
                        AddUrl($"/Home/Services?city={Uri.EscapeDataString(city)}&service={Uri.EscapeDataString(prob)}", "weekly", "0.80");
                    }
                }
            }
            catch
            {
                // Fallback graceful handling
            }

            sitemapContent.AppendLine("</urlset>");
            Response.Headers["Cache-Control"] = "public, max-age=3600";
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
            robots.AppendLine("Allow: /Home/");
            robots.AppendLine("Disallow: /Admin/");
            robots.AppendLine("Disallow: /Customer/");
            robots.AppendLine("Disallow: /Mechanic/");
            robots.AppendLine("Disallow: /Auth/");
            robots.AppendLine($"Sitemap: {scheme}://{host}/sitemap.xml");

            return Content(robots.ToString(), "text/plain", System.Text.Encoding.UTF8);
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveNotifications()
        {
            string role = "Guest";
            string? userCity = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin")) 
                {
                    role = "Admin";
                }
                else if (User.IsInRole("Mechanic"))
                {
                    role = "Mechanic";
                    string? userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdStr, out int mechId))
                    {
                        var mechProfile = await _dbContext.MechanicProfiles.FirstOrDefaultAsync(p => p.UserId == mechId);
                        if (mechProfile != null && !string.IsNullOrEmpty(mechProfile.City))
                        {
                            userCity = mechProfile.City;
                        }
                    }
                }
                else if (User.IsInRole("Customer"))
                {
                    role = "Customer";
                    if (Request.Cookies.TryGetValue("UserSelectedCity", out string? cookieCity))
                    {
                        userCity = cookieCity;
                    }
                }
            }
            else
            {
                if (Request.Cookies.TryGetValue("UserSelectedCity", out string? cookieCity))
                {
                    userCity = cookieCity;
                }
            }

            var nowUtc = DateTime.UtcNow;

            // Notification is active if it has not expired yet (or has no expiration set)
            var query = _dbContext.PushNotificationLogs.Where(n => n.ExpiresAt == null || n.ExpiresAt > nowUtc);

            if (role == "Customer")
            {
                query = query.Where(n => n.TargetAudience == "All Users" || n.TargetAudience == "Customers" || n.TargetAudience == "Homepage Visitors" || n.TargetAudience == "Homepage");
            }
            else if (role == "Mechanic")
            {
                query = query.Where(n => n.TargetAudience == "All Users" || n.TargetAudience == "Mechanics");
            }
            else if (role == "Admin")
            {
                query = query.Where(n => n.TargetAudience == "All Users" || n.TargetAudience == "Customers" || n.TargetAudience == "Mechanics" || n.TargetAudience == "Homepage Visitors" || n.TargetAudience == "Homepage");
            }
            else
            {
                query = query.Where(n => n.TargetAudience == "All Users" || n.TargetAudience == "Homepage Visitors" || n.TargetAudience == "Homepage" || n.TargetAudience == "Customers");
            }

            var allNotifs = await query.OrderByDescending(n => n.SentAt).ToListAsync();

            var filteredNotifs = allNotifs.Where(n =>
                string.IsNullOrEmpty(n.SelectedCity) ||
                n.SelectedCity.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                n.SelectedCity.Equals("All India Cities", StringComparison.OrdinalIgnoreCase) ||
                (userCity != null && n.SelectedCity.Equals(userCity, StringComparison.OrdinalIgnoreCase))
            ).Select(n => new
            {
                n.Id,
                n.Title,
                n.Message,
                n.TargetAudience,
                SentAt = n.SentAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt")
            }).ToList();

            return Json(new { success = true, notifications = filteredNotifs });
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
