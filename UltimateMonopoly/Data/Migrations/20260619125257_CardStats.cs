using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimateMonopoly.Data.Migrations
{
    /// <inheritdoc />
    public partial class CardStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "CardsNeverPlayed",
                table: "PlayerGameStats",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<string>(
                name: "CardsPlayedByTypeJson",
                table: "PlayerGameStats",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CardsTakenByTypeJson",
                table: "PlayerGameStats",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<uint>(
                name: "ImmunityCardsPlayed",
                table: "PlayerGameStats",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "ImmunityCardsTaken",
                table: "PlayerGameStats",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "InstantPlayCards",
                table: "PlayerGameStats",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<int>(
                name: "MostPlayedEngagement",
                table: "PlayerGameStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MostPlayedTrigger",
                table: "PlayerGameStats",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "TotalCardsPlayed",
                table: "PlayerGameStats",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "TotalCardsTaken",
                table: "PlayerGameStats",
                type: "int unsigned",
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CardsNeverPlayed",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "CardsPlayedByTypeJson",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "CardsTakenByTypeJson",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "ImmunityCardsPlayed",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "ImmunityCardsTaken",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "InstantPlayCards",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "MostPlayedEngagement",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "MostPlayedTrigger",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "TotalCardsPlayed",
                table: "PlayerGameStats");

            migrationBuilder.DropColumn(
                name: "TotalCardsTaken",
                table: "PlayerGameStats");
        }
    }
}
