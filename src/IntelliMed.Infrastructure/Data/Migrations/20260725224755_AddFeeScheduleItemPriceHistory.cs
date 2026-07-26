using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeeScheduleItemPriceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeeScheduleItemPriceHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FeeScheduleItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Fee = table.Column<decimal>(type: "TEXT", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeeScheduleItemPriceHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FeeScheduleItemPriceHistories_FeeScheduleItems_FeeScheduleItemId",
                        column: x => x.FeeScheduleItemId,
                        principalTable: "FeeScheduleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeeScheduleItemPriceHistories_FeeScheduleItemId_ChangedAt",
                table: "FeeScheduleItemPriceHistories",
                columns: new[] { "FeeScheduleItemId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeeScheduleItemPriceHistories");
        }
    }
}
