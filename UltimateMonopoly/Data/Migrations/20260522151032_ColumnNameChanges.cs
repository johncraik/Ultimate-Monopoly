using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UltimateMonopoly.Data.Migrations
{
    /// <inheritdoc />
    public partial class ColumnNameChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PropertyColour",
                table: "BoardSkinSpaces",
                newName: "PropertySet");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PropertySet",
                table: "BoardSkinSpaces",
                newName: "PropertyColour");
        }
    }
}
