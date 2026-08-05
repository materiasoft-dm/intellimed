using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicineReferenceData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    AmtSubstanceId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsAmtConfirmed = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveIngredients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Medicines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    GenericName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Strength = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Form = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsPbsListed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ArtgId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    AmtConceptId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PbsItemCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    AtcCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MedicineIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MedicineId = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveIngredientId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicineIngredients_ActiveIngredients_ActiveIngredientId",
                        column: x => x.ActiveIngredientId,
                        principalTable: "ActiveIngredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MedicineIngredients_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveIngredients_Name",
                table: "ActiveIngredients",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicineIngredients_ActiveIngredientId",
                table: "MedicineIngredients",
                column: "ActiveIngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicineIngredients_MedicineId_ActiveIngredientId",
                table: "MedicineIngredients",
                columns: new[] { "MedicineId", "ActiveIngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_AmtConceptId",
                table: "Medicines",
                column: "AmtConceptId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_Name",
                table: "Medicines",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicineIngredients");

            migrationBuilder.DropTable(
                name: "ActiveIngredients");

            migrationBuilder.DropTable(
                name: "Medicines");
        }
    }
}
