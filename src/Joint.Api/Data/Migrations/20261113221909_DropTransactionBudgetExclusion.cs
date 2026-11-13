using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropTransactionBudgetExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExcludedFromBudget",
                schema: "joint",
                table: "Transactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ExcludedFromBudget",
                schema: "joint",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
