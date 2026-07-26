using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDerivedItemEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DerivedQuantity",
                table: "InvoiceItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DerivedItemConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BillingItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    CalculationType = table.Column<int>(type: "INTEGER", nullable: false),
                    AssociatedBillingItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    Percentage = table.Column<decimal>(type: "TEXT", nullable: true),
                    UnitValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    LimitQuantity = table.Column<decimal>(type: "TEXT", nullable: true),
                    OverLimitUnitValue = table.Column<decimal>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DerivedItemConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DerivedItemConfigs_BillingItems_AssociatedBillingItemId",
                        column: x => x.AssociatedBillingItemId,
                        principalTable: "BillingItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DerivedItemConfigs_BillingItems_BillingItemId",
                        column: x => x.BillingItemId,
                        principalTable: "BillingItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DerivedItemConfigs_AssociatedBillingItemId",
                table: "DerivedItemConfigs",
                column: "AssociatedBillingItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DerivedItemConfigs_BillingItemId",
                table: "DerivedItemConfigs",
                column: "BillingItemId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DerivedItemConfigs");

            migrationBuilder.DropColumn(
                name: "DerivedQuantity",
                table: "InvoiceItems");
        }
    }
}
