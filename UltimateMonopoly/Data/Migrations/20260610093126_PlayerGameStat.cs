using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimateMonopoly.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlayerGameStat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerGameStats",
                columns: table => new
                {
                    GameId = table.Column<string>(type: "varchar(38)", maxLength: 38, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserId = table.Column<string>(type: "varchar(38)", maxLength: 38, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedById = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LastModifiedById = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastModifiedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedById = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DeletedUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RestoredById = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RestoredUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PlayerId = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MoneyEarned = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneySpent = table.Column<uint>(type: "int unsigned", nullable: false),
                    LargestSinglePayment = table.Column<uint>(type: "int unsigned", nullable: false),
                    LargestSinglePaymentReason = table.Column<int>(type: "int", nullable: true),
                    LargestSinglePaymentPropertyIndex = table.Column<ushort>(type: "smallint unsigned", nullable: true),
                    LargestRentPayment = table.Column<uint>(type: "int unsigned", nullable: true),
                    LargestRentPaymentPropertyIndex = table.Column<ushort>(type: "smallint unsigned", nullable: true),
                    SpentAcquiringProperty = table.Column<uint>(type: "int unsigned", nullable: false),
                    SpentBuilding = table.Column<uint>(type: "int unsigned", nullable: false),
                    SpentUnmortgaging = table.Column<uint>(type: "int unsigned", nullable: false),
                    SpentOnFines = table.Column<uint>(type: "int unsigned", nullable: false),
                    SpentOnLeavingJail = table.Column<uint>(type: "int unsigned", nullable: false),
                    SpentOnRepayingLoans = table.Column<uint>(type: "int unsigned", nullable: false),
                    RentPaid = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyGivenInDeals = table.Column<uint>(type: "int unsigned", nullable: false),
                    RentEarned = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesPassedGo = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyCollectedFromGo = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyFromSelling = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyFromCards = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyFromMortgaging = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyFromFreeParking = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyFromTriples = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyFromSnakeEyes = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyFromDiceNumber = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyFromDeals = table.Column<uint>(type: "int unsigned", nullable: false),
                    MoneyFromBankruptPlayers = table.Column<uint>(type: "int unsigned", nullable: false),
                    MostProfitablePropertyIndex = table.Column<ushort>(type: "smallint unsigned", nullable: true),
                    LeastProfitablePropertyIndex = table.Column<ushort>(type: "smallint unsigned", nullable: true),
                    MostProfitablePropertySet = table.Column<int>(type: "int", nullable: true),
                    LeastProfitablePropertySet = table.Column<int>(type: "int", nullable: true),
                    MaxCompleteSets = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    MaxCompleteSetsTurnNumber = table.Column<uint>(type: "int unsigned", nullable: false),
                    TotalPropertiesAcquired = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    TotalPropertiesLost = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    PropertiesPurged = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    TotalTurnRolls = table.Column<uint>(type: "int unsigned", nullable: false),
                    TotalCardRolls = table.Column<uint>(type: "int unsigned", nullable: false),
                    DoublesRolled = table.Column<uint>(type: "int unsigned", nullable: false),
                    TriplesRolled = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesSomeoneRolledYourDiceNumber = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesYouRolledYourDiceNumber = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesChangedDirection = table.Column<uint>(type: "int unsigned", nullable: false),
                    TotalDistanceTraveledClockwise = table.Column<long>(type: "bigint", nullable: false),
                    TotalDistanceTraveledCounterClockwise = table.Column<long>(type: "bigint", nullable: false),
                    MostLandedOnBoardIndex = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    TimesLandedOnGo = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesLandedOnFreeParking = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesLandedOnTax = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesSentToJail = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesLeftJailByPaying = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesLeftJailByPlayingCard = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesLeftJailByDice = table.Column<uint>(type: "int unsigned", nullable: false),
                    TotalJailTurns = table.Column<uint>(type: "int unsigned", nullable: false),
                    TotalPropertiesHandedInFP = table.Column<uint>(type: "int unsigned", nullable: false),
                    TotalPropertiesTakenFromFP = table.Column<uint>(type: "int unsigned", nullable: false),
                    FPHandedInSetTypesJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TotalLoansTaken = table.Column<uint>(type: "int unsigned", nullable: false),
                    TotalLoanAmountTaken = table.Column<uint>(type: "int unsigned", nullable: false),
                    TotalLoanRepayments = table.Column<uint>(type: "int unsigned", nullable: false),
                    TotalLoansRepaid = table.Column<uint>(type: "int unsigned", nullable: false),
                    OutstandingLoanDebt = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesMortgaged = table.Column<uint>(type: "int unsigned", nullable: false),
                    TimesUnmortgaged = table.Column<uint>(type: "int unsigned", nullable: false),
                    MortgageFeesPaid = table.Column<uint>(type: "int unsigned", nullable: false),
                    Bankrupted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    VoluntaryBankruptcy = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BankruptedByAmount = table.Column<uint>(type: "int unsigned", nullable: true),
                    TurnsSurvived = table.Column<int>(type: "int", nullable: false),
                    FinalBalance = table.Column<uint>(type: "int unsigned", nullable: false),
                    FinalNetWorth = table.Column<long>(type: "bigint", nullable: false),
                    PeakNetWorth = table.Column<long>(type: "bigint", nullable: false),
                    PeakNetWorthTurnNumber = table.Column<int>(type: "int", nullable: false),
                    PeakBalance = table.Column<uint>(type: "int unsigned", nullable: false),
                    PeakBalanceTurnNumber = table.Column<int>(type: "int", nullable: false),
                    BalanceOverTimeJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NetWorthOverTimeJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PropertyCountOverTimeJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WealthRankOverTimeJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerGameStats", x => new { x.GameId, x.UserId });
                    table.ForeignKey(
                        name: "FK_PlayerGameStats_GamePlayers_GameId_UserId",
                        columns: x => new { x.GameId, x.UserId },
                        principalTable: "GamePlayers",
                        principalColumns: new[] { "GameId", "UserId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlayerGameStats_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerGameStats");
        }
    }
}
