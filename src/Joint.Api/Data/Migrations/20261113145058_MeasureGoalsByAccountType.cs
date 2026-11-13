using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MeasureGoalsByAccountType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Goals_Accounts_AccountId",
                schema: "joint",
                table: "Goals");

            migrationBuilder.DropIndex(
                name: "IX_Goals_AccountId",
                schema: "joint",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "AccountId",
                schema: "joint",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "joint",
                table: "Goals");

            migrationBuilder.DropColumn(
                name: "SavedAmount",
                schema: "joint",
                table: "Goals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                schema: "joint",
                table: "Goals",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "joint",
                table: "Goals",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SavedAmount",
                schema: "joint",
                table: "Goals",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Goals_AccountId",
                schema: "joint",
                table: "Goals",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_Goals_Accounts_AccountId",
                schema: "joint",
                table: "Goals",
                column: "AccountId",
                principalSchema: "joint",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
