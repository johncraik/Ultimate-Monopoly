using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimateMonopoly.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResolutonColToReportedUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Resolution",
                table: "ReportedUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "ReportedUsers");
        }
    }
}
