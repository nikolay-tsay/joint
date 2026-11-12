using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankConnections_HouseholdMembers_MemberId",
                schema: "joint",
                table: "BankConnections");

            migrationBuilder.DropTable(
                name: "HouseholdMembers",
                schema: "joint");

            migrationBuilder.RenameColumn(
                name: "MemberId",
                schema: "joint",
                table: "BankConnections",
                newName: "OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_BankConnections_MemberId",
                schema: "joint",
                table: "BankConnections",
                newName: "IX_BankConnections_OwnerUserId");

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "joint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "joint",
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HouseholdInvitations",
                schema: "joint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "joint",
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Users_AcceptedByUserId",
                        column: x => x.AcceptedByUserId,
                        principalSchema: "joint",
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HouseholdInvitations_Users_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalSchema: "joint",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_AcceptedByUserId",
                schema: "joint",
                table: "HouseholdInvitations",
                column: "AcceptedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_CodeHash",
                schema: "joint",
                table: "HouseholdInvitations",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_HouseholdId",
                schema: "joint",
                table: "HouseholdInvitations",
                column: "HouseholdId");

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdInvitations_InvitedByUserId",
                schema: "joint",
                table: "HouseholdInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_HouseholdId",
                schema: "joint",
                table: "Users",
                column: "HouseholdId");

            migrationBuilder.AddForeignKey(
                name: "FK_BankConnections_Users_OwnerUserId",
                schema: "joint",
                table: "BankConnections",
                column: "OwnerUserId",
                principalSchema: "joint",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BankConnections_Users_OwnerUserId",
                schema: "joint",
                table: "BankConnections");

            migrationBuilder.DropTable(
                name: "HouseholdInvitations",
                schema: "joint");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "joint");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                schema: "joint",
                table: "BankConnections",
                newName: "MemberId");

            migrationBuilder.RenameIndex(
                name: "IX_BankConnections_OwnerUserId",
                schema: "joint",
                table: "BankConnections",
                newName: "IX_BankConnections_MemberId");

            migrationBuilder.CreateTable(
                name: "HouseholdMembers",
                schema: "joint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HouseholdMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HouseholdMembers_Households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "joint",
                        principalTable: "Households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HouseholdMembers_HouseholdId_UserId",
                schema: "joint",
                table: "HouseholdMembers",
                columns: new[] { "HouseholdId", "UserId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_BankConnections_HouseholdMembers_MemberId",
                schema: "joint",
                table: "BankConnections",
                column: "MemberId",
                principalSchema: "joint",
                principalTable: "HouseholdMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
