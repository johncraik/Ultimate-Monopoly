using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimateMonopoly.Data.Migrations
{
    /// <inheritdoc />
    public partial class StatTableUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "MostLandedOnBoardIndexCount",
                table: "PlayerGameStats",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "SpentOnTurnTax",
                table: "PlayerGameStats",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MostLandedOnBoardIndexCount",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "SpentOnTurnTax",
                table: "PlayerGameStats");
        }
    }
}
