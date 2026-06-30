using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimateMonopoly.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegisteredUtcOnAppUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RegisteredUtc",
                table: "AspNetUsers",
                type: "datetime(6)",
                nullable: true);

            // Backfill existing accounts: treat their last login as their registration date — the best signal
            // we have, since no real registration time was recorded before this column. Never-logged-in
            // accounts keep a null RegisteredUtc.
            migrationBuilder.Sql(
                "UPDATE AspNetUsers SET RegisteredUtc = LastLoginUtc WHERE LastLoginUtc IS NOT NULL;");

            migrationBuilder.CreateTable(
                name: "DailyActivityStats",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TotalUsers = table.Column<int>(type: "int", nullable: false),
                    NewUsers = table.Column<int>(type: "int", nullable: false),
                    Logins = table.Column<int>(type: "int", nullable: false),
                    Dau = table.Column<int>(type: "int", nullable: false),
                    Wau = table.Column<int>(type: "int", nullable: false),
                    Mau = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyActivityStats", x => x.Date);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyActivityStats");

            migrationBuilder.DropColumn(
                name: "RegisteredUtc",
                table: "AspNetUsers");
        }
    }
}
