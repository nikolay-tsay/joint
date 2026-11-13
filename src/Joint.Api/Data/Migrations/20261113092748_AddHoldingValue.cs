using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHoldingValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnrealizedProfitLoss",
                schema: "joint",
                table: "Holdings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Value",
                schema: "joint",
                table: "Holdings",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnrealizedProfitLoss",
                schema: "joint",
                table: "Holdings");

            migrationBuilder.DropColumn(
                name: "Value",
                schema: "joint",
                table: "Holdings");
        }
    }
}
