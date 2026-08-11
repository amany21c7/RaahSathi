using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaahSathi.Migrations
{
    /// <inheritdoc />
    public partial class AddJobBookingDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FuelType",
                table: "Jobs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Landmark",
                table: "Jobs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProblemDescription",
                table: "Jobs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FuelType",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "Landmark",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "ProblemDescription",
                table: "Jobs");
        }
    }
}
