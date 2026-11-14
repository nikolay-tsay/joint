using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "BudgetPeriodFrom",
                schema: "joint",
                table: "Households",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "BudgetPeriodTo",
                schema: "joint",
                table: "Households",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetPeriodFrom",
                schema: "joint",
                table: "Households");

            migrationBuilder.DropColumn(
                name: "BudgetPeriodTo",
                schema: "joint",
                table: "Households");
        }
    }
}
