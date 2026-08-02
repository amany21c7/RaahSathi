using Microsoft.EntityFrameworkCore;
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
builder.Services.AddScoped<IPricingRepository, PricingRepository>();
builder.Services.AddScoped<IPricingService, PricingService>();

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
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

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
                IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Vehicles]') AND name = N'VehiclePhotoUrl')
                BEGIN
                    ALTER TABLE [Vehicles] ADD [VehiclePhotoUrl] nvarchar(500) NOT NULL DEFAULT '';
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
                        [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
                    );
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

                IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[AuditLogs]') AND type in (N'U'))
                BEGIN
                    CREATE TABLE [AuditLogs] (
                        [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        [AdminName] nvarchar(100) NOT NULL DEFAULT 'Super Admin',
                        [ActionType] nvarchar(100) NOT NULL DEFAULT 'UPDATE',
                        [Details] nvarchar(max) NOT NULL,
                        [TimeStamp] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        [IpAddress] nvarchar(50) NOT NULL DEFAULT '127.0.0.1',
                        [UserAgent] nvarchar(200) NOT NULL DEFAULT 'Chrome Browser'
                    );
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
                        -- Idempotency Guard: If job is already Completed, exit early
                        IF EXISTS (SELECT 1 FROM dbo.Jobs WHERE Id = @JobId AND Status = N'Completed')
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

                        DECLARE @Phase1 FLOAT = 8.0, @Phase2 FLOAT = 10.0, @Phase3 FLOAT = 12.0, @PartsComm FLOAT = 5.0;
                        
                        SELECT @Phase1 = TRY_CAST(SettingValue AS FLOAT) FROM dbo.AdminSystemSettings WHERE SettingKey = 'CommissionPhase1';
                        SELECT @Phase2 = TRY_CAST(SettingValue AS FLOAT) FROM dbo.AdminSystemSettings WHERE SettingKey = 'CommissionPhase2';
                        SELECT @Phase3 = TRY_CAST(SettingValue AS FLOAT) FROM dbo.AdminSystemSettings WHERE SettingKey = 'CommissionPhase3';
                        SELECT @PartsComm = TRY_CAST(SettingValue AS FLOAT) FROM dbo.AdminSystemSettings WHERE SettingKey = 'CommissionParts';

                        SET @Phase1 = ISNULL(@Phase1, 8.0) / 100.0;
                        SET @Phase2 = ISNULL(@Phase2, 10.0) / 100.0;
                        SET @Phase3 = ISNULL(@Phase3, 12.0) / 100.0;
                        SET @PartsComm = ISNULL(@PartsComm, 5.0) / 100.0;

                        DECLARE @PartsAmt FLOAT = 0.0, @PartsApproved BIT = 0;
                        SELECT @PartsAmt = ISNULL(PartsEstimateAmount, 0), @PartsApproved = PartsApproved FROM dbo.Jobs WHERE Id = @JobId;
                        IF @PartsApproved IS NULL OR @PartsApproved = 0 SET @PartsAmt = 0.0;

                        DECLARE @ServiceAmt FLOAT = @FinalBill - @PartsAmt;
                        IF @ServiceAmt < 0 SET @ServiceAmt = 0.0;

                        DECLARE @ServiceComm FLOAT = 0.0, @ServiceRate FLOAT = 0.08;
                        IF @ServiceAmt < 1000
                        BEGIN
                            SET @ServiceRate = @Phase1;
                            SET @ServiceComm = @ServiceAmt * @Phase1;
                        END
                        ELSE IF @ServiceAmt <= 3000
                        BEGIN
                            SET @ServiceRate = @Phase2;
                            SET @ServiceComm = @ServiceAmt * @Phase2;
                        END
                        ELSE
                        BEGIN
                            SET @ServiceRate = @Phase3;
                            SET @ServiceComm = @ServiceAmt * @Phase3;
                        END

                        DECLARE @PartsCommAmt FLOAT = @PartsAmt * @PartsComm;
                        DECLARE @AdminCommission FLOAT = ROUND(@ServiceComm + @PartsCommAmt, 2);
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
        }
        catch (Exception exSchema)
        {
            var loggerSchema = services.GetRequiredService<ILogger<Program>>();
            loggerSchema.LogWarning(exSchema, "Schema update warning.");
        }
        

        
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
                new PricingRule { VehicleCategory = "Commercial", BaseFee = 199, PerKmRate = 12, BaseTowingFee = 1000, PerKmTowingRate = 40 },
                new PricingRule { VehicleCategory = "Heavy", BaseFee = 299, PerKmRate = 15, BaseTowingFee = 2500, PerKmTowingRate = 80 }
            };
            context.PricingRules.AddRange(rules);
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
                new ProblemTypePricing { ProblemName = "Heavy Vehicle Hydraulic & Engine Repair", VehicleCategory = "Heavy", MinServiceCharge = 1000, MaxServiceCharge = 8000 }
            };
            context.ProblemTypePricings.AddRange(problemTypes);
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
