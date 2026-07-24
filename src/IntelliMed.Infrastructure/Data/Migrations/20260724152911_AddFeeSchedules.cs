using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeeSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    HealthFundId = table.Column<int>(type: "INTEGER", nullable: true),
                    FeeTableId = table.Column<int>(type: "INTEGER", nullable: true),
                    RoundingType = table.Column<int>(type: "INTEGER", nullable: false),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeSchedules_FeeSchedules_FeeTableId",
                        column: x => x.FeeTableId,
                        principalTable: "FeeSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeeSchedules_HealthFunds_HealthFundId",
                        column: x => x.HealthFundId,
                        principalTable: "HealthFunds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeSchedules_Code",
                table: "FeeSchedules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeeSchedules_FeeTableId",
                table: "FeeSchedules",
                column: "FeeTableId");

            migrationBuilder.CreateIndex(
                name: "IX_FeeSchedules_HealthFundId",
                table: "FeeSchedules",
                column: "HealthFundId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeeSchedules");
        }
    }
}
