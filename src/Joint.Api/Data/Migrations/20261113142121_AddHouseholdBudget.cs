using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdBudget : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyBudget",
                schema: "joint",
                table: "Households",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MonthlyBudget",
                schema: "joint",
                table: "Households");
        }
    }
}
