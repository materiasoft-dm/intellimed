using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescribingSafetyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientAllergies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveIngredientId = table.Column<int>(type: "INTEGER", nullable: true),
                    Substance = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Reaction = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Severity = table.Column<int>(type: "INTEGER", nullable: true),
                    OnsetDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsArchived = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientAllergies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientAllergies_ActiveIngredients_ActiveIngredientId",
                        column: x => x.ActiveIngredientId,
                        principalTable: "ActiveIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ClientAllergies_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrugInteractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IngredientAId = table.Column<int>(type: "INTEGER", nullable: false),
                    IngredientBId = table.Column<int>(type: "INTEGER", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SourceDataset = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugInteractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrugInteractions_ActiveIngredients_IngredientAId",
                        column: x => x.IngredientAId,
                        principalTable: "ActiveIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrugInteractions_ActiveIngredients_IngredientBId",
                        column: x => x.IngredientBId,
                        principalTable: "ActiveIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientAllergies_ActiveIngredientId",
                table: "ClientAllergies",
                column: "ActiveIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientAllergies_ClientId",
                table: "ClientAllergies",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugInteractions_IngredientAId_IngredientBId",
                table: "DrugInteractions",
                columns: new[] { "IngredientAId", "IngredientBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrugInteractions_IngredientBId",
                table: "DrugInteractions",
                column: "IngredientBId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientAllergies");

            migrationBuilder.DropTable(
                name: "DrugInteractions");
        }
    }
}
