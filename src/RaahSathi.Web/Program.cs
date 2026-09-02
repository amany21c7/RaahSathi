using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using RaahSathi.Data;
using RaahSathi.Services;
using RaahSathi.Models;
using RaahSathi.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext with SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Business Services & Repositories
builder.Services.AddScoped<IPricingEngine, PricingEngine>();
builder.Services.AddScoped<IDispatchEngine, DispatchEngine>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPricingRepository, PricingRepository>();
builder.Services.AddScoped<IPricingService, PricingService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddHttpContextAccessor();

// Persist Data Protection Encryption Keys to Database (logins survive deployments and restarts)
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApplicationDbContext>()
    .SetApplicationName("RaahSathi");

// Add Cookie Authentication
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// Add services to the container.
var mvcBuilder = builder.Services.AddControllersWithViews();
if (builder.Environment.IsDevelopment())
{
    mvcBuilder.AddRazorRuntimeCompilation();
}

// Add Native Tiered Rate Limiting (Protects auth, bookings, and global traffic while ensuring zero disruption for live polling)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.Append("Retry-After", "15");

        bool isAjaxOrApi = context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                           context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json") ||
                           context.HttpContext.Request.Path.Value?.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) == true ||
                           context.HttpContext.Request.Path.Value?.Contains("/GetLive", StringComparison.OrdinalIgnoreCase) == true ||
                           context.HttpContext.Request.Path.Value?.Contains("/GetTelemetry", StringComparison.OrdinalIgnoreCase) == true;

        if (isAjaxOrApi)
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync("{\"success\":false,\"message\":\"Too many requests. Please slow down and wait a moment.\",\"statusCode\":429,\"retryAfter\":15}", cancellationToken);
        }
        else
        {
            context.HttpContext.Response.ContentType = "text/html";
            await context.HttpContext.Response.WriteAsync(@"
                <!DOCTYPE html>
                <html lang='en'>
                <head>
                    <meta charset='utf-8'><meta name='viewport' content='width=device-width, initial-scale=1'>
                    <title>429 - Too Many Requests | RaahSathi</title>
                    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css'>
                </head>
                <body class='bg-dark text-white d-flex align-items-center justify-content-center min-vh-100 p-3'>
                    <div class='card bg-black border-warning text-center p-4 shadow-lg' style='max-width: 480px;'>
                        <div class='mb-3 text-warning fs-1'>⚠️</div>
                        <h4 class='text-warning fw-bold mb-2'>Too Many Requests</h4>
                        <p class='text-muted small mb-4'>We noticed unusual traffic spikes. To keep RaahSathi emergency services secure and fast, please wait 15 seconds before trying again.</p>
                        <a href='javascript:location.reload();' class='btn btn-warning btn-sm font-weight-bold'>Refresh Page</a>
                    </div>
                </body>
                </html>", cancellationToken);
        }
    };

    // 1. Strict Auth & OTP Policy (10 req/min per IP) - Brute-force & OTP abuse protection
    options.AddPolicy("auth-policy", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon_auth_ip";
        return System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(ip, _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    // 2. High-Capacity Live Polling Policy (120 req/min with sliding window) - Zero disruption for 3s/5s polling
    options.AddPolicy("live-polling-policy", httpContext =>
    {
        var partitionKey = httpContext.User.Identity?.IsAuthenticated == true 
            ? $"user_{httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}" 
            : (httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon_polling_ip");

        return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 2,
            AutoReplenishment = true
        });
    });

    // 3. Action & Booking Policy (Anti-Spam / Anti-Duplicate Click Token Bucket)
    options.AddPolicy("booking-action-policy", httpContext =>
    {
        var partitionKey = httpContext.User.Identity?.IsAuthenticated == true 
            ? $"action_user_{httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value}" 
            : (httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon_action_ip");

        return System.Threading.RateLimiting.RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new System.Threading.RateLimiting.TokenBucketRateLimiterOptions
        {
            TokenLimit = 15,
            TokensPerPeriod = 3,
            ReplenishmentPeriod = TimeSpan.FromSeconds(5),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    // 4. Global Fallback Policy for general browsing (150 req/min)
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        // Bypass rate limiting for static assets
        var path = httpContext.Request.Path.Value ?? "";
        if (path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase))
        {
            return System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("static_assets");
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon_global_ip";
        return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
        {
            PermitLimit = 150,
            Window = TimeSpan.FromMinutes(1),
            SegmentsPerWindow = 6,
            QueueLimit = 5,
            AutoReplenishment = true
        });
    });
});

var app = builder.Build();

// Automatically ensure DB is created and seeded on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.EnsureCreated(); // Creates RaahSathiDb on SQL Server
        
        // Auto-create missing columns & tables for existing database
        try
        {
            context.Database.ExecuteSqlRaw(@"
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SystemApiSettings]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [SystemApiSettings] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [SmsApiKey] nvarchar(500) NOT NULL DEFAULT '',
                        [WhatsAppBusinessNumber] nvarchar(100) NOT NULL DEFAULT '',
                        [GoogleMapsApiKey] nvarchar(500) NOT NULL DEFAULT '',
                        [SmtpSenderEmail] nvarchar(255) NOT NULL DEFAULT '',
                        [UpdatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[SystemContactSettings]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [SystemContactSettings] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [HelplineNumber] nvarchar(100) NOT NULL DEFAULT '+91 9891819236',
                        [TollFreeNumber] nvarchar(100) NOT NULL DEFAULT '1800-102-7224',
                        [EmergencySupportNumber] nvarchar(100) NOT NULL DEFAULT '+91 9536838103',
                        [WhatsAppNumber] nvarchar(100) NOT NULL DEFAULT '+91 9891819236',
                        [SupportEmail] nvarchar(255) NOT NULL DEFAULT 'support.raahsathi@gmail.com',
                        [BillingEmail] nvarchar(255) NOT NULL DEFAULT 'billing@raahsathi.in',
                        [PartnerHelplineNumber] nvarchar(100) NOT NULL DEFAULT '+91 9891819236',
                        [OfficeAddress] nvarchar(500) NOT NULL DEFAULT 'Tower B, DLF Cyber City, Sector 24, Gurugram, Haryana - 122002',
                        [UpdatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[DataProtectionKeys]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [DataProtectionKeys] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [FriendlyName] nvarchar(max) NULL,
                        [Xml] nvarchar(max) NULL
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Vehicles]') AND name = N'VehiclePhotoUrl')
                BEGIN
                    ALTER TABLE [Vehicles] ADD [VehiclePhotoUrl] nvarchar(500) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'FuelType')
                BEGIN
                    ALTER TABLE [Jobs] ADD [FuelType] nvarchar(50) NOT NULL DEFAULT 'Petrol';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'BatteryType')
                BEGIN
                    ALTER TABLE [Jobs] ADD [BatteryType] nvarchar(50) NOT NULL DEFAULT 'Don''t Know';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'IsEmergencyCharging')
                BEGIN
                    ALTER TABLE [Jobs] ADD [IsEmergencyCharging] bit NOT NULL DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'TowingNeeded')
                BEGIN
                    ALTER TABLE [Jobs] ADD [TowingNeeded] bit NOT NULL DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'TowingCharge')
                BEGIN
                    ALTER TABLE [Jobs] ADD [TowingCharge] float NOT NULL DEFAULT 0.0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'TowingReason')
                BEGIN
                    ALTER TABLE [Jobs] ADD [TowingReason] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'TowingProofPhoto')
                BEGIN
                    ALTER TABLE [Jobs] ADD [TowingProofPhoto] nvarchar(500) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'TowingApproved')
                BEGIN
                    ALTER TABLE [Jobs] ADD [TowingApproved] bit NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'PartsEstimateAmount')
                BEGIN
                    ALTER TABLE [Jobs] ADD [PartsEstimateAmount] float NOT NULL DEFAULT 0.0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'PartsEstimateDetails')
                BEGIN
                    ALTER TABLE [Jobs] ADD [PartsEstimateDetails] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'PartsApproved')
                BEGIN
                    ALTER TABLE [Jobs] ADD [PartsApproved] bit NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'FinalBillAmount')
                BEGIN
                    ALTER TABLE [Jobs] ADD [FinalBillAmount] float NOT NULL DEFAULT 0.0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'DisputeStatus')
                BEGIN
                    ALTER TABLE [Jobs] ADD [DisputeStatus] nvarchar(50) NOT NULL DEFAULT 'None';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'DisputeReason')
                BEGIN
                    ALTER TABLE [Jobs] ADD [DisputeReason] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'DisputeResolution')
                BEGIN
                    ALTER TABLE [Jobs] ADD [DisputeResolution] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'RatingFromCustomer')
                BEGIN
                    ALTER TABLE [Jobs] ADD [RatingFromCustomer] float NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'FeedbackFromCustomer')
                BEGIN
                    ALTER TABLE [Jobs] ADD [FeedbackFromCustomer] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'RatingFromMechanic')
                BEGIN
                    ALTER TABLE [Jobs] ADD [RatingFromMechanic] float NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'FeedbackFromMechanic')
                BEGIN
                    ALTER TABLE [Jobs] ADD [FeedbackFromMechanic] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'TotalReviewsCount')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [TotalReviewsCount] int NOT NULL DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'RecommendedCount')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [RecommendedCount] int NOT NULL DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'RecommendationPercentage')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [RecommendationPercentage] int NOT NULL DEFAULT 98;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'SuccessRatePercentage')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [SuccessRatePercentage] int NOT NULL DEFAULT 95;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'AvgArrivalTimeMins')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [AvgArrivalTimeMins] int NOT NULL DEFAULT 18;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'DateOfBirth')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [DateOfBirth] datetime2 NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'PositiveFeedbackTags')
                BEGIN
                    ALTER TABLE [Jobs] ADD [PositiveFeedbackTags] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'IsRecommended')
                BEGIN
                    ALTER TABLE [Jobs] ADD [IsRecommended] bit NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'ReviewPhotoUrl')
                BEGIN
                    ALTER TABLE [Jobs] ADD [ReviewPhotoUrl] nvarchar(500) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'RatedAt')
                BEGIN
                    ALTER TABLE [Jobs] ADD [RatedAt] datetime2 NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'IsFlaggedByAdmin')
                BEGIN
                    ALTER TABLE [Jobs] ADD [IsFlaggedByAdmin] bit NOT NULL DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'DeclinedMechanicIds')
                BEGIN
                    ALTER TABLE [Jobs] ADD [DeclinedMechanicIds] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'ProblemPhotoUrl')
                BEGIN
                    ALTER TABLE [Jobs] ADD [ProblemPhotoUrl] nvarchar(500) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ProblemTypePricings]') AND name = N'CityName')
                BEGIN
                    ALTER TABLE [ProblemTypePricings] ADD [CityName] nvarchar(100) NOT NULL DEFAULT 'All Cities';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[PricingRules]') AND name = N'CityName')
                BEGIN
                    ALTER TABLE [PricingRules] ADD [CityName] nvarchar(100) NOT NULL DEFAULT 'All Cities';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CityServiceAreas]') AND name = N'IsEmergencyMode')
                BEGIN
                    ALTER TABLE [CityServiceAreas] ADD [IsEmergencyMode] bit NOT NULL DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CityServiceAreas]') AND name = N'EmergencyReason')
                BEGIN
                    ALTER TABLE [CityServiceAreas] ADD [EmergencyReason] nvarchar(200) NOT NULL DEFAULT 'Heavy Rain 🌧️';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'IsSimulationPaused')
                BEGIN
                    ALTER TABLE [Jobs] ADD [IsSimulationPaused] bit NOT NULL DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'LastMovementTime')
                BEGIN
                    ALTER TABLE [Jobs] ADD [LastMovementTime] datetime2 NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'LastLocationUpdateTime')
                BEGIN
                    ALTER TABLE [Jobs] ADD [LastLocationUpdateTime] datetime2 NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'CustomEstimateAmount')
                BEGIN
                    ALTER TABLE [Jobs] ADD [CustomEstimateAmount] float NOT NULL DEFAULT 0.0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'CustomEstimateDetails')
                BEGIN
                    ALTER TABLE [Jobs] ADD [CustomEstimateDetails] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'CustomEstimateApproved')
                BEGIN
                    ALTER TABLE [Jobs] ADD [CustomEstimateApproved] bit NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'ExtraPartsName')
                BEGIN
                    ALTER TABLE [Jobs] ADD [ExtraPartsName] nvarchar(max) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'ExtraLabourCharge')
                BEGIN
                    ALTER TABLE [Jobs] ADD [ExtraLabourCharge] float NOT NULL DEFAULT 0.0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = N'PartsMrp')
                BEGIN
                    ALTER TABLE [Jobs] ADD [PartsMrp] float NOT NULL DEFAULT 0.0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'Languages')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [Languages] nvarchar(200) NOT NULL DEFAULT 'Hindi, English';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'DrivingLicenceUrl')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [DrivingLicenceUrl] nvarchar(500) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'WorkingHours')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [WorkingHours] nvarchar(100) NOT NULL DEFAULT '9:00 AM - 9:00 PM';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'BankName')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [BankName] nvarchar(100) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'BankAccountNumber')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [BankAccountNumber] nvarchar(50) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'IfscCode')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [IfscCode] nvarchar(20) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'PreferredPayoutMethod')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [PreferredPayoutMethod] nvarchar(50) NOT NULL DEFAULT 'UPI';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'AcceptsCash')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [AcceptsCash] bit NOT NULL DEFAULT 1;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'AcceptanceRatePercentage')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [AcceptanceRatePercentage] int NOT NULL DEFAULT 96;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'CancellationRatePercentage')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [CancellationRatePercentage] int NOT NULL DEFAULT 1;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'RepeatCustomersCount')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [RepeatCustomersCount] int NOT NULL DEFAULT 14;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'AccountHolderName')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [AccountHolderName] nvarchar(200) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = N'City')
                BEGIN
                    ALTER TABLE [MechanicProfiles] ADD [City] nvarchar(100) NOT NULL DEFAULT 'Noida';
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[MechanicPayoutRequests]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [MechanicPayoutRequests] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [MechanicId] int NOT NULL,
                        [Amount] float NOT NULL,
                        [PayoutMethod] nvarchar(50) NOT NULL DEFAULT 'Bank',
                        [AccountHolderName] nvarchar(200) NOT NULL DEFAULT '',
                        [BankAccountNumber] nvarchar(100) NOT NULL DEFAULT '',
                        [BankName] nvarchar(200) NOT NULL DEFAULT '',
                        [IfscCode] nvarchar(50) NOT NULL DEFAULT '',
                        [UpiId] nvarchar(100) NOT NULL DEFAULT '',
                        [Status] nvarchar(50) NOT NULL DEFAULT 'Pending',
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        [ProcessedAt] datetime2 NULL,
                        [AdminRemarks] nvarchar(500) NOT NULL DEFAULT '',
                        [TransactionReference] nvarchar(100) NOT NULL DEFAULT ''
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[MechanicComplaints]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [MechanicComplaints] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [JobId] int NOT NULL,
                        [CustomerId] int NOT NULL,
                        [MechanicId] int NOT NULL,
                        [Rating] float NOT NULL,
                        [SelectedReasons] nvarchar(500) NOT NULL DEFAULT '',
                        [Category] nvarchar(100) NOT NULL DEFAULT 'General',
                        [CustomerDetails] nvarchar(1000) NOT NULL DEFAULT '',
                        [Status] nvarchar(50) NOT NULL DEFAULT 'Pending',
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[MechanicSupportMessages]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [MechanicSupportMessages] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [MechanicId] int NOT NULL,
                        [Title] nvarchar(200) NOT NULL DEFAULT 'Support Notification',
                        [MessageText] nvarchar(2000) NOT NULL DEFAULT '',
                        [SenderRole] nvarchar(50) NOT NULL DEFAULT 'Admin',
                        [SenderName] nvarchar(100) NOT NULL DEFAULT 'RaahSathi Support Team',
                        [IsFromAdmin] bit NOT NULL DEFAULT 1,
                        [IsRead] bit NOT NULL DEFAULT 0,
                        [SentAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[ContactMessages]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [ContactMessages] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [FullName] nvarchar(200) NOT NULL,
                        [Phone] nvarchar(50) NOT NULL,
                        [Email] nvarchar(200) NOT NULL DEFAULT '',
                        [Subject] nvarchar(200) NOT NULL DEFAULT 'General Inquiry',
                        [Message] nvarchar(2000) NOT NULL,
                        [Status] nvarchar(50) NOT NULL DEFAULT 'New',
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        [PhotoUrl] nvarchar(500) NOT NULL DEFAULT '',
                        [UserRole] nvarchar(50) NOT NULL DEFAULT 'Guest'
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ContactMessages]') AND name = N'PhotoUrl')
                BEGIN
                    ALTER TABLE [ContactMessages] ADD [PhotoUrl] nvarchar(500) NOT NULL DEFAULT '';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[ContactMessages]') AND name = N'UserRole')
                BEGIN
                    ALTER TABLE [ContactMessages] ADD [UserRole] nvarchar(50) NOT NULL DEFAULT 'Guest';
                END;

                 IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[JobChatMessages]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [JobChatMessages] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [JobId] int NOT NULL,
                        [SenderId] int NOT NULL,
                        [SenderRole] nvarchar(20) NOT NULL DEFAULT 'Customer',
                        [SenderName] nvarchar(100) NOT NULL DEFAULT '',
                        [MessageText] nvarchar(1000) NOT NULL DEFAULT '',
                        [SentAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        [IsRead] bit NOT NULL DEFAULT 0
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[JobChatMessages]') AND name = N'IsRead')
                BEGIN
                    ALTER TABLE [JobChatMessages] ADD [IsRead] bit NOT NULL DEFAULT 0;
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[ProblemTypePricings]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [ProblemTypePricings] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [ProblemName] nvarchar(150) NOT NULL,
                        [VehicleCategory] nvarchar(50) NOT NULL DEFAULT 'Car',
                        [MinServiceCharge] float NOT NULL DEFAULT 150.0,
                        [MaxServiceCharge] float NOT NULL DEFAULT 3500.0,
                        [IsActive] bit NOT NULL DEFAULT 1
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[AdminWithdrawals]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [AdminWithdrawals] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [Amount] float NOT NULL,
                        [PayoutMethod] nvarchar(100) NOT NULL DEFAULT 'Bank Transfer',
                        [ReferenceNumber] nvarchar(100) NOT NULL DEFAULT '',
                        [WithdrawnAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Payments]') AND name = N'AdminCommissionAmount')
                BEGIN
                    ALTER TABLE [Payments] ADD [AdminCommissionAmount] float NOT NULL DEFAULT 0.0;
                    ALTER TABLE [Payments] ADD [MechanicEarningAmount] float NOT NULL DEFAULT 0.0;
                    ALTER TABLE [Payments] ADD [CommissionRateUsed] float NOT NULL DEFAULT 0.08;
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Users]') AND name = N'IsBlocked')
                BEGIN
                    ALTER TABLE [Users] ADD [IsBlocked] bit NOT NULL DEFAULT 0;
                    ALTER TABLE [Users] ADD [AdminRole] nvarchar(100) NOT NULL DEFAULT 'Super Admin';
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[CityServiceAreas]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [CityServiceAreas] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [State] nvarchar(100) NOT NULL DEFAULT 'Uttar Pradesh',
                        [CityName] nvarchar(100) NOT NULL DEFAULT 'Noida',
                        [AreaName] nvarchar(150) NOT NULL DEFAULT 'Sector 62',
                        [ServiceRadiusKm] float NOT NULL DEFAULT 15.0,
                        [IsActive] bit NOT NULL DEFAULT 1
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[CustomServices]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [CustomServices] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [ServiceName] nvarchar(150) NOT NULL,
                        [IconClass] nvarchar(100) NOT NULL DEFAULT 'fa-screwdriver-wrench',
                        [Category] nvarchar(100) NOT NULL DEFAULT 'Breakdown',
                        [BasePrice] float NOT NULL DEFAULT 199.0,
                        [MaxPrice] float NOT NULL DEFAULT 499.0,
                        [Description] nvarchar(500) NOT NULL DEFAULT '',
                        [IsActive] bit NOT NULL DEFAULT 1
                    );
                END;

                IF (SELECT COUNT(*) FROM [CustomServices]) = 0
                BEGIN
                    INSERT INTO [CustomServices] ([ServiceName], [IconClass], [Category], [BasePrice], [MaxPrice], [Description], [IsActive])
                    VALUES 
                    (N'EV Mobile Quick Charge', N'fa-charging-station', N'EV Support', 599.0, 1499.0, N'On-demand battery jump and quick charge for electric vehicles using mobile generator vans.', 1),
                    (N'Heavy Mud Winch Recovery', N'fa-truck-monster', N'Towing & Recovery', 999.0, 3999.0, N'Off-road or deep mud vehicle extraction using heavy duty industrial winch pulls.', 1),
                    (N'Emergency Fuel Delivery', N'fa-gas-pump', N'Emergency Breakdown', 249.0, 599.0, N'Delivering 5 Liters of emergency Petrol/Diesel straight to your breakdown location.', 1);
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[CmsBanners]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [CmsBanners] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [Title] nvarchar(200) NOT NULL,
                        [ImageUrl] nvarchar(500) NOT NULL,
                        [TargetPage] nvarchar(100) NOT NULL DEFAULT 'Homepage',
                        [IsActive] bit NOT NULL DEFAULT 1,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CmsBanners]') AND name = N'TargetAudience')
                BEGIN
                    ALTER TABLE [CmsBanners] ADD [TargetAudience] nvarchar(100) NOT NULL DEFAULT 'All Users';
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[CmsBanners]') AND name = N'ExpiresAt')
                BEGIN
                    ALTER TABLE [CmsBanners] ADD [ExpiresAt] datetime2 NULL;
                END;

                -- Cleanup any invalid local paths or quoted strings in CmsBanners
                DELETE FROM [CmsBanners] WHERE [ImageUrl] LIKE '%C:%' OR [ImageUrl] LIKE '%Downloads%' OR [ImageUrl] LIKE '%' + CHAR(34) + '%';


                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[PushNotificationLogs]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [PushNotificationLogs] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [TargetAudience] nvarchar(100) NOT NULL DEFAULT 'All Users',
                        [SelectedCity] nvarchar(100) NOT NULL DEFAULT 'All',
                        [Title] nvarchar(200) NOT NULL,
                        [Message] nvarchar(max) NOT NULL,
                        [SentCount] int NOT NULL DEFAULT 1,
                        [SentAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[PushNotificationLogs]') AND name = N'ExpiresAt')
                BEGIN
                    ALTER TABLE [PushNotificationLogs] ADD [ExpiresAt] datetime2 NULL;
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[AuditLogs]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [AuditLogs] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [AdminName] nvarchar(100) NOT NULL DEFAULT 'Super Admin',
                        [ActionType] nvarchar(100) NOT NULL DEFAULT 'UPDATE',
                        [Details] nvarchar(max) NOT NULL,
                        [TimeStamp] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        [IpAddress] nvarchar(50) NOT NULL DEFAULT '127.0.0.1',
                        [UserAgent] nvarchar(200) NOT NULL DEFAULT 'Chrome Browser',
                        [UserRole] nvarchar(50) NOT NULL DEFAULT 'Admin'
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[AuditLogs]') AND name = N'UserRole')
                BEGIN
                    ALTER TABLE [AuditLogs] ADD [UserRole] nvarchar(50) NOT NULL DEFAULT 'Admin';
                END;

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[AdminSystemSettings]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [AdminSystemSettings] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [SettingKey] nvarchar(100) NOT NULL,
                        [SettingValue] nvarchar(max) NOT NULL,
                        [Category] nvarchar(100) NOT NULL DEFAULT 'General',
                        [Description] nvarchar(500) NOT NULL DEFAULT ''
                    );
                END;

                -- Ensure multi-problem & partial cancellation columns exist on Jobs table
                ALTER TABLE [Jobs] ALTER COLUMN [ProblemType] NVARCHAR(MAX) NOT NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = 'SelectedProblemsJson')
                    ALTER TABLE [Jobs] ADD [SelectedProblemsJson] NVARCHAR(MAX) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = 'CancelledProblemItem')
                    ALTER TABLE [Jobs] ADD [CancelledProblemItem] NVARCHAR(200) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = 'ProblemCancelReason')
                    ALTER TABLE [Jobs] ADD [ProblemCancelReason] NVARCHAR(500) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = 'ProblemCancelDescription')
                    ALTER TABLE [Jobs] ADD [ProblemCancelDescription] NVARCHAR(1000) NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Jobs]') AND name = 'ProblemCancelledAt')
                    ALTER TABLE [Jobs] ADD [ProblemCancelledAt] DATETIME2 NULL;

                -- Mechanic Monthly Subscriptions
                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[MechanicSubscriptions]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [MechanicSubscriptions] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [MechanicId] int NOT NULL,
                        [Amount] float NOT NULL DEFAULT 0.0,
                        [StartDate] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        [EndDate] datetime2 NOT NULL DEFAULT DATEADD(day, 30, GETUTCDATE()),
                        [PaymentStatus] nvarchar(50) NOT NULL DEFAULT 'Success',
                        [RazorpayPaymentId] nvarchar(100) NULL,
                        [RazorpayOrderId] nvarchar(100) NULL,
                        [Notes] nvarchar(500) NULL,
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
                END;

                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = 'SubscriptionValidTill')
                    ALTER TABLE [MechanicProfiles] ADD [SubscriptionValidTill] DATETIME2 NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = 'SubscriptionAmountPaid')
                    ALTER TABLE [MechanicProfiles] ADD [SubscriptionAmountPaid] FLOAT NOT NULL DEFAULT 0.0;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = 'SubscriptionLastPaidAt')
                    ALTER TABLE [MechanicProfiles] ADD [SubscriptionLastPaidAt] DATETIME2 NULL;
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[MechanicProfiles]') AND name = 'SubscriptionStatus')
                    ALTER TABLE [MechanicProfiles] ADD [SubscriptionStatus] NVARCHAR(50) NOT NULL DEFAULT 'Trial';

                -- Stored Procedure: rs_payments_process_escrow
                IF OBJECT_ID(N'[dbo].[rs_payments_process_escrow]', N'P') IS NOT NULL
                    DROP PROCEDURE [dbo].[rs_payments_process_escrow];
            ");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE PROCEDURE dbo.rs_payments_process_escrow
                    @JobId INT,
                    @PaymentId NVARCHAR(100)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    BEGIN TRANSACTION;
                    BEGIN TRY
                        -- Idempotency Guard: If payment is already released, exit early
                        IF EXISTS (SELECT 1 FROM dbo.Payments WHERE JobId = @JobId AND PaymentStatus = N'Released')
                        BEGIN
                            COMMIT TRANSACTION;
                            RETURN;
                        END

                        DECLARE @FinalBill FLOAT, @CustomerId INT, @MechanicId INT;
                        DECLARE @VisitingCharge FLOAT, @ServiceMin FLOAT, @BaseEst FLOAT;

                        SELECT @FinalBill = FinalBillAmount, 
                               @CustomerId = CustomerId, 
                               @MechanicId = MechanicId,
                               @VisitingCharge = VisitingCharge,
                               @ServiceMin = ServiceChargeMin
                        FROM dbo.Jobs WHERE Id = @JobId;

                        SET @BaseEst = ISNULL(@VisitingCharge, 0) + ISNULL(@ServiceMin, 0);
                        IF @FinalBill < @BaseEst SET @FinalBill = @BaseEst;

                        DECLARE @Phase1 FLOAT = 8.0, @Phase2 FLOAT = 10.0, @Phase3 FLOAT = 12.0, @PartsComm FLOAT = 5.0, @CustomComm FLOAT = 0.0;
                        
                        SELECT @Phase1 = TRY_CAST(SettingValue AS FLOAT) FROM dbo.AdminSystemSettings WHERE SettingKey = 'CommissionPhase1';
                        SELECT @Phase2 = TRY_CAST(SettingValue AS FLOAT) FROM dbo.AdminSystemSettings WHERE SettingKey = 'CommissionPhase2';
                        SELECT @Phase3 = TRY_CAST(SettingValue AS FLOAT) FROM dbo.AdminSystemSettings WHERE SettingKey = 'CommissionPhase3';
                        SELECT @PartsComm = TRY_CAST(SettingValue AS FLOAT) FROM dbo.AdminSystemSettings WHERE SettingKey = 'CommissionParts';
                        SELECT @CustomComm = TRY_CAST(SettingValue AS FLOAT) FROM dbo.AdminSystemSettings WHERE SettingKey = 'CommissionCustomRepair';

                        SET @Phase1 = ISNULL(@Phase1, 8.0) / 100.0;
                        SET @Phase2 = ISNULL(@Phase2, 10.0) / 100.0;
                        SET @Phase3 = ISNULL(@Phase3, 12.0) / 100.0;
                        SET @PartsComm = ISNULL(@PartsComm, 5.0) / 100.0;
                        SET @CustomComm = ISNULL(@CustomComm, 0.0) / 100.0;

                        DECLARE @PartsAmt FLOAT = 0.0, @PartsApproved BIT = 0;
                        SELECT @PartsAmt = ISNULL(PartsEstimateAmount, 0), @PartsApproved = PartsApproved FROM dbo.Jobs WHERE Id = @JobId;
                        IF @PartsApproved IS NULL OR @PartsApproved = 0 SET @PartsAmt = 0.0;

                        DECLARE @CustomAmt FLOAT = 0.0, @CustomApproved BIT = 0;
                        SELECT @CustomAmt = ISNULL(CustomEstimateAmount, 0), @CustomApproved = CustomEstimateApproved FROM dbo.Jobs WHERE Id = @JobId;
                        IF @CustomApproved IS NULL OR @CustomApproved = 0 SET @CustomAmt = 0.0;

                        DECLARE @ServiceComm FLOAT = 0.0, @ServiceRate FLOAT = 0.08;
                        IF @FinalBill < 1000
                        BEGIN
                            SET @ServiceRate = @Phase1;
                            SET @ServiceComm = @FinalBill * @Phase1;
                        END
                        ELSE IF @FinalBill <= 3000
                        BEGIN
                            SET @ServiceRate = @Phase2;
                            SET @ServiceComm = @FinalBill * @Phase2;
                        END
                        ELSE
                        BEGIN
                            SET @ServiceRate = @Phase3;
                            SET @ServiceComm = @FinalBill * @Phase3;
                        END

                        DECLARE @PartsCommAmt FLOAT = @PartsAmt * @PartsComm;
                        DECLARE @CustomCommAmt FLOAT = CASE WHEN @CustomAmt > 0 AND @CustomComm > 0 THEN (@CustomAmt * @CustomComm) ELSE 0.0 END;
                        DECLARE @AdminCommission FLOAT = ROUND(@ServiceComm + @PartsCommAmt + @CustomCommAmt, 2);
                        DECLARE @MechanicEarning FLOAT = ROUND(@FinalBill - @AdminCommission, 2);
                        DECLARE @CommRate FLOAT = CASE WHEN @FinalBill > 0 THEN ROUND(@AdminCommission / @FinalBill, 4) ELSE @ServiceRate END;

                        -- If payment is cash, mechanic receives full bill in hand, so digital wallet is debited by commission amount
                        DECLARE @ActualEarning FLOAT;
                        IF @PaymentId LIKE N'pay_cash_%'
                        BEGIN
                            SET @ActualEarning = -@AdminCommission;
                        END
                        ELSE
                        BEGIN
                            SET @ActualEarning = @MechanicEarning;
                        END

                        IF EXISTS (SELECT 1 FROM dbo.Payments WHERE JobId = @JobId)
                        BEGIN
                            UPDATE dbo.Payments
                            SET Amount = @FinalBill,
                                PaymentStatus = N'Released',
                                RazorpayPaymentId = @PaymentId,
                                AdminCommissionAmount = @AdminCommission,
                                MechanicEarningAmount = @ActualEarning,
                                CommissionRateUsed = @CommRate
                            WHERE JobId = @JobId;
                        END
                        ELSE
                        BEGIN
                            INSERT INTO dbo.Payments (JobId, Amount, PaymentStatus, RazorpayPaymentId, AdminCommissionAmount, MechanicEarningAmount, CommissionRateUsed, CreatedAt)
                            VALUES (@JobId, @FinalBill, N'Released', @PaymentId, @AdminCommission, @ActualEarning, @CommRate, GETUTCDATE());
                        END

                        IF @MechanicId IS NOT NULL
                        BEGIN
                            UPDATE dbo.MechanicProfiles
                            SET CurrentEarnings = CurrentEarnings + @ActualEarning,
                                TotalJobs = TotalJobs + 1,
                                CommissionRate = @CommRate
                            WHERE UserId = @MechanicId;
                        END

                        UPDATE dbo.Jobs
                        SET Status = N'Completed', CompletedAt = GETUTCDATE()
                        WHERE Id = @JobId;

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH
                END;
            ");

            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID(N'[dbo].[rs_adminwithdrawals_insert]', N'P') IS NOT NULL
                    DROP PROCEDURE [dbo].[rs_adminwithdrawals_insert];
            ");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE PROCEDURE dbo.rs_adminwithdrawals_insert
                    @Amount FLOAT,
                    @PayoutMethod NVARCHAR(100),
                    @ReferenceNumber NVARCHAR(100)
                AS
                BEGIN
                    SET NOCOUNT ON;
                    BEGIN TRANSACTION;
                    BEGIN TRY
                        INSERT INTO dbo.AdminWithdrawals (Amount, PayoutMethod, ReferenceNumber, WithdrawnAt)
                        VALUES (@Amount, @PayoutMethod, @ReferenceNumber, GETUTCDATE());

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH
                END;
            ");

            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID(N'[dbo].[rs_mechanicprofiles_withdraw_wallet]', N'P') IS NOT NULL
                    DROP PROCEDURE [dbo].[rs_mechanicprofiles_withdraw_wallet];
            ");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE PROCEDURE dbo.rs_mechanicprofiles_withdraw_wallet
                    @MechanicUserId INT,
                    @Amount FLOAT
                AS
                BEGIN
                    SET NOCOUNT ON;
                    BEGIN TRANSACTION;
                    BEGIN TRY
                        UPDATE dbo.MechanicProfiles
                        SET CurrentEarnings = CurrentEarnings - @Amount
                        WHERE UserId = @MechanicUserId;

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH
                END;
            ");

            await context.Database.ExecuteSqlRawAsync(@"
                IF OBJECT_ID(N'[dbo].[rs_mechanicprofiles_update_bank_details]', N'P') IS NOT NULL
                    DROP PROCEDURE [dbo].[rs_mechanicprofiles_update_bank_details];
            ");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE PROCEDURE dbo.rs_mechanicprofiles_update_bank_details
                    @MechanicUserId INT,
                    @PreferredPayoutMethod NVARCHAR(50),
                    @UpiId NVARCHAR(100) = NULL,
                    @AccountHolderName NVARCHAR(200) = NULL,
                    @BankName NVARCHAR(200) = NULL,
                    @BankAccountNumber NVARCHAR(100) = NULL,
                    @IfscCode NVARCHAR(50) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;
                    BEGIN TRANSACTION;
                    BEGIN TRY
                        IF EXISTS (SELECT 1 FROM dbo.MechanicProfiles WITH (UPDLOCK) WHERE UserId = @MechanicUserId)
                        BEGIN
                            UPDATE dbo.MechanicProfiles
                            SET PreferredPayoutMethod = @PreferredPayoutMethod,
                                UpiId = @UpiId,
                                AccountHolderName = @AccountHolderName,
                                BankName = @BankName,
                                BankAccountNumber = @BankAccountNumber,
                                IfscCode = @IfscCode
                            WHERE UserId = @MechanicUserId;
                        END

                        COMMIT TRANSACTION;
                    END TRY
                    BEGIN CATCH
                        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
                        THROW;
                    END CATCH
                END;
            ");
        }
        catch (Exception exSchema)
        {
            var loggerSchema = services.GetRequiredService<ILogger<Program>>();
            loggerSchema.LogWarning(exSchema, "Schema update warning.");
        }

        try
        {
            var contact = context.SystemContactSettings.FirstOrDefault();
            if (contact != null)
            {
                ContactInfoHelper.Initialize(contact);
            }
            else
            {
                ContactInfoHelper.Initialize(context.AdminSystemSettings.ToList());
            }
        }
        catch { }

        // Initial Seed for empty database: Only Admin & Pricing Rules
        var admin = context.Users.FirstOrDefault(u => u.PhoneNumber == "9536838103" || u.Role == "Admin");
        if (admin == null)
        {
            admin = new User 
            { 
                Name = "Aman yadav", 
                PhoneNumber = "9536838103", 
                Role = "Admin", 
                Password = PasswordHasher.HashPassword("aman1234") 
            };
            context.Users.Add(admin);
        }
        else
        {
            admin.Name = "Aman yadav";
            admin.PhoneNumber = "9536838103";
            admin.Role = "Admin";
            admin.Password = PasswordHasher.HashPassword("aman1234");
        }
        context.SaveChanges();

        if (!context.PricingRules.Any())
        {
            // 2. Pricing Rules
            var rules = new List<PricingRule>
            {
                new PricingRule { VehicleCategory = "Car", BaseFee = 99, PerKmRate = 8, BaseTowingFee = 500, PerKmTowingRate = 20 },
                new PricingRule { VehicleCategory = "2-Wheeler", BaseFee = 49, PerKmRate = 5, BaseTowingFee = 200, PerKmTowingRate = 10 },
                new PricingRule { VehicleCategory = "E-Rickshaw", BaseFee = 69, PerKmRate = 6, BaseTowingFee = 250, PerKmTowingRate = 12 },
                new PricingRule { VehicleCategory = "Auto-Rickshaw", BaseFee = 79, PerKmRate = 7, BaseTowingFee = 300, PerKmTowingRate = 14 },
                new PricingRule { VehicleCategory = "Commercial", BaseFee = 199, PerKmRate = 12, BaseTowingFee = 1000, PerKmTowingRate = 40 },
                new PricingRule { VehicleCategory = "Heavy", BaseFee = 299, PerKmRate = 15, BaseTowingFee = 2500, PerKmTowingRate = 80 }
            };
            context.PricingRules.AddRange(rules);
            context.SaveChanges();
        }
        else if (!context.PricingRules.Any(r => r.VehicleCategory == "E-Rickshaw"))
        {
            context.PricingRules.Add(new PricingRule { VehicleCategory = "E-Rickshaw", BaseFee = 69, PerKmRate = 6, BaseTowingFee = 250, PerKmTowingRate = 12 });
            context.PricingRules.Add(new PricingRule { VehicleCategory = "Auto-Rickshaw", BaseFee = 79, PerKmRate = 7, BaseTowingFee = 300, PerKmTowingRate = 14 });
            context.SaveChanges();
        }

        if (!context.ProblemTypePricings.Any())
        {
            var problemTypes = new List<ProblemTypePricing>
            {
                new ProblemTypePricing { ProblemName = "Battery jump-start/replacement", VehicleCategory = "Car", MinServiceCharge = 150, MaxServiceCharge = 3500 },
                new ProblemTypePricing { ProblemName = "Flat Tyre / Puncture Repair", VehicleCategory = "Car", MinServiceCharge = 200, MaxServiceCharge = 800 },
                new ProblemTypePricing { ProblemName = "Emergency Fuel Delivery", VehicleCategory = "Car", MinServiceCharge = 250, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Towing Assistance", VehicleCategory = "Car", MinServiceCharge = 300, MaxServiceCharge = 1500 },
                new ProblemTypePricing { ProblemName = "General Engine / Mechanical Checkup", VehicleCategory = "Car", MinServiceCharge = 180, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Key locked inside / Lockout", VehicleCategory = "Car", MinServiceCharge = 200, MaxServiceCharge = 600 },
                new ProblemTypePricing { ProblemName = "Brake & Clutch Repair", VehicleCategory = "Car", MinServiceCharge = 200, MaxServiceCharge = 1000 },
                new ProblemTypePricing { ProblemName = "Gearbox & Transmission Repair", VehicleCategory = "Car", MinServiceCharge = 400, MaxServiceCharge = 3000 },
                new ProblemTypePricing { ProblemName = "Suspension & Shocker Repair", VehicleCategory = "Car", MinServiceCharge = 350, MaxServiceCharge = 2500 },
                new ProblemTypePricing { ProblemName = "2-Wheeler Puncture / Chain Repair", VehicleCategory = "2-Wheeler", MinServiceCharge = 80, MaxServiceCharge = 300 },
                new ProblemTypePricing { ProblemName = "2-Wheeler Spark Plug & Battery", VehicleCategory = "2-Wheeler", MinServiceCharge = 100, MaxServiceCharge = 800 },
                new ProblemTypePricing { ProblemName = "Commercial Air Brake / Tyre Repair", VehicleCategory = "Commercial", MinServiceCharge = 500, MaxServiceCharge = 4000 },
                new ProblemTypePricing { ProblemName = "Heavy Vehicle Hydraulic & Engine Repair", VehicleCategory = "Heavy", MinServiceCharge = 1000, MaxServiceCharge = 8000 },
                
                // E-Rickshaw Specialized Problems
                new ProblemTypePricing { ProblemName = "Battery Dead / Low Battery", VehicleCategory = "E-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1800 },
                new ProblemTypePricing { ProblemName = "Emergency EV Charging", VehicleCategory = "E-Rickshaw", MinServiceCharge = 250, MaxServiceCharge = 800 },
                new ProblemTypePricing { ProblemName = "Controller Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 350, MaxServiceCharge = 1800 },
                new ProblemTypePricing { ProblemName = "BLDC Motor Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 400, MaxServiceCharge = 2500 },
                new ProblemTypePricing { ProblemName = "Puncture / Tyre Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 99, MaxServiceCharge = 350 },
                new ProblemTypePricing { ProblemName = "Wiring / Electrical Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 750 },
                new ProblemTypePricing { ProblemName = "Vehicle Not Moving", VehicleCategory = "E-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Ignition / Switch Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 100, MaxServiceCharge = 450 },
                new ProblemTypePricing { ProblemName = "Battery Overheating", VehicleCategory = "E-Rickshaw", MinServiceCharge = 200, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Don't Know (On-Spot EV Diagnostic)", VehicleCategory = "E-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1000 },
                
                // Auto-Rickshaw Problem Pricing
                new ProblemTypePricing { ProblemName = "Battery Dead", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 120, MaxServiceCharge = 1500 },
                new ProblemTypePricing { ProblemName = "CNG / Fuel Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 950 },
                new ProblemTypePricing { ProblemName = "Puncture Repair", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 80, MaxServiceCharge = 300 },
                new ProblemTypePricing { ProblemName = "Engine Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 250, MaxServiceCharge = 2200 },
                new ProblemTypePricing { ProblemName = "Clutch / Gear Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 200, MaxServiceCharge = 1600 },
                new ProblemTypePricing { ProblemName = "Vehicle Not Starting", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Overheating", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 180, MaxServiceCharge = 900 },
                new ProblemTypePricing { ProblemName = "Electrical Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 800 },
                new ProblemTypePricing { ProblemName = "General Mechanical Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Don't Know (Auto Diagnosis)", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1000 }
            };
            context.ProblemTypePricings.AddRange(problemTypes);
            context.SaveChanges();
        }
        else if (!context.ProblemTypePricings.Any(p => p.VehicleCategory == "E-Rickshaw"))
        {
            var evProblems = new List<ProblemTypePricing>
            {
                new ProblemTypePricing { ProblemName = "Battery Dead / Low Battery", VehicleCategory = "E-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1800 },
                new ProblemTypePricing { ProblemName = "Emergency EV Charging", VehicleCategory = "E-Rickshaw", MinServiceCharge = 250, MaxServiceCharge = 800 },
                new ProblemTypePricing { ProblemName = "Controller Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 350, MaxServiceCharge = 1800 },
                new ProblemTypePricing { ProblemName = "BLDC Motor Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 400, MaxServiceCharge = 2500 },
                new ProblemTypePricing { ProblemName = "Puncture / Tyre Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 99, MaxServiceCharge = 350 },
                new ProblemTypePricing { ProblemName = "Wiring / Electrical Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 750 },
                new ProblemTypePricing { ProblemName = "Vehicle Not Moving", VehicleCategory = "E-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Ignition / Switch Problem", VehicleCategory = "E-Rickshaw", MinServiceCharge = 100, MaxServiceCharge = 450 },
                new ProblemTypePricing { ProblemName = "Battery Overheating", VehicleCategory = "E-Rickshaw", MinServiceCharge = 200, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Don't Know (On-Spot EV Diagnostic)", VehicleCategory = "E-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1000 },
                
                // Auto-Rickshaw Problem Pricing
                new ProblemTypePricing { ProblemName = "Battery Dead", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 120, MaxServiceCharge = 1500 },
                new ProblemTypePricing { ProblemName = "CNG / Fuel Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 950 },
                new ProblemTypePricing { ProblemName = "Puncture Repair", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 80, MaxServiceCharge = 300 },
                new ProblemTypePricing { ProblemName = "Engine Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 250, MaxServiceCharge = 2200 },
                new ProblemTypePricing { ProblemName = "Clutch / Gear Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 200, MaxServiceCharge = 1600 },
                new ProblemTypePricing { ProblemName = "Vehicle Not Starting", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Overheating", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 180, MaxServiceCharge = 900 },
                new ProblemTypePricing { ProblemName = "Electrical Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 800 },
                new ProblemTypePricing { ProblemName = "General Mechanical Problem", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1200 },
                new ProblemTypePricing { ProblemName = "Don't Know (Auto Diagnosis)", VehicleCategory = "Auto-Rickshaw", MinServiceCharge = 150, MaxServiceCharge = 1000 }
            };
            context.ProblemTypePricings.AddRange(evProblems);
            context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred creating or seeding the SQLite database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "sitemap",
    pattern: "sitemap.xml",
    defaults: new { controller = "Home", action = "Sitemap" });

app.MapControllerRoute(
    name: "robots",
    pattern: "robots.txt",
    defaults: new { controller = "Home", action = "RobotsText" });

app.MapControllerRoute(
    name: "adminSecretLogin",
    pattern: "AdminRaahiSathiLogin",
    defaults: new { controller = "Auth", action = "AdminRahiSarhiLogin" });

app.MapControllerRoute(
    name: "adminSecretLoginAlt",
    pattern: "AdminRahiSarhiLogin",
    defaults: new { controller = "Auth", action = "AdminRahiSarhiLogin" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
