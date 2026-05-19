using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimateMonopoly.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenamedReportedUserKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportedUsers_BlockedUsers_BlockedUserId",
                table: "ReportedUsers");

            migrationBuilder.RenameColumn(
                name: "BlockedUserId",
                table: "ReportedUsers",
                newName: "BlockedId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportedUsers_BlockedUsers_BlockedId",
                table: "ReportedUsers",
                column: "BlockedId",
                principalTable: "BlockedUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportedUsers_BlockedUsers_BlockedId",
                table: "ReportedUsers");

            migrationBuilder.RenameColumn(
                name: "BlockedId",
                table: "ReportedUsers",
                newName: "BlockedUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ReportedUsers_BlockedUsers_BlockedUserId",
                table: "ReportedUsers",
                column: "BlockedUserId",
                principalTable: "BlockedUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
