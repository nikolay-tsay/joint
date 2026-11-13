using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Joint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryRules",
                schema: "joint",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MatchKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryRules_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "joint",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryRules_UserId_MatchKey",
                schema: "joint",
                table: "CategoryRules",
                columns: new[] { "UserId", "MatchKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryRules",
                schema: "joint");
        }
    }
}
