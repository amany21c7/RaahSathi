using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaahSathi.Migrations
{
    /// <inheritdoc />
    public partial class AddMechanicKycFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BaseFee = table.Column<double>(type: "float", nullable: false),
                    PerKmRate = table.Column<double>(type: "float", nullable: false),
                    BaseTowingFee = table.Column<double>(type: "float", nullable: false),
                    PerKmTowingRate = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MechanicProfiles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    IsOnline = table.Column<bool>(type: "bit", nullable: false),
                    Latitude = table.Column<double>(type: "float", nullable: false),
                    Longitude = table.Column<double>(type: "float", nullable: false),
                    Rating = table.Column<double>(type: "float", nullable: false),
                    TotalJobs = table.Column<int>(type: "int", nullable: false),
                    KycStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AadhaarNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Gender = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProfilePhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AadhaarFrontUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AadhaarBackUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PanCardUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SelfieUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ShopName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShopPhotoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ShopAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Pincode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ShopTiming = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsCertified = table.Column<bool>(type: "bit", nullable: false),
                    GarageName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VehicleExpertise = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Specialization = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ServiceRadiusKm = table.Column<int>(type: "int", nullable: false),
                    SkillCategory = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExperienceYears = table.Column<int>(type: "int", nullable: false),
                    CommissionRate = table.Column<double>(type: "float", nullable: false),
                    CurrentEarnings = table.Column<double>(type: "float", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MechanicProfiles", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_MechanicProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    VehicleType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vehicles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vehicles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    MechanicId = table.Column<int>(type: "int", nullable: true),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    ProblemType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CustomerLat = table.Column<double>(type: "float", nullable: false),
                    CustomerLng = table.Column<double>(type: "float", nullable: false),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VisitingCharge = table.Column<double>(type: "float", nullable: false),
                    ServiceChargeMin = table.Column<double>(type: "float", nullable: false),
                    ServiceChargeMax = table.Column<double>(type: "float", nullable: false),
                    PartsEstimateAmount = table.Column<double>(type: "float", nullable: false),
                    PartsEstimateDetails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PartsApproved = table.Column<bool>(type: "bit", nullable: true),
                    TowingNeeded = table.Column<bool>(type: "bit", nullable: false),
                    TowingCharge = table.Column<double>(type: "float", nullable: false),
                    TowingReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TowingProofPhoto = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TowingApproved = table.Column<bool>(type: "bit", nullable: true),
                    FinalBillAmount = table.Column<double>(type: "float", nullable: false),
                    DisputeStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DisputeReason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisputeResolution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RatingFromCustomer = table.Column<double>(type: "float", nullable: true),
                    FeedbackFromCustomer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RatingFromMechanic = table.Column<double>(type: "float", nullable: true),
                    FeedbackFromMechanic = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Jobs_Users_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jobs_Users_MechanicId",
                        column: x => x.MechanicId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Jobs_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<double>(type: "float", nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RazorpayPaymentId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CustomerId",
                table: "Jobs",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_MechanicId",
                table: "Jobs",
                column: "MechanicId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_VehicleId",
                table: "Jobs",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_JobId",
                table: "Payments",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Vehicles_UserId",
                table: "Vehicles",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MechanicProfiles");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PricingRules");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "Vehicles");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
