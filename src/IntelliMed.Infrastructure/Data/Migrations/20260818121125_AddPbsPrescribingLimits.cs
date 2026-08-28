using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPbsPrescribingLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSection100",
                table: "Medicines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxQuantity",
                table: "Medicines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRepeats",
                table: "Medicines",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "Medicines",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TherapeuticGroupId",
                table: "Medicines",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TherapeuticGroupTitle",
                table: "Medicines",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Medicines_TherapeuticGroupId",
                table: "Medicines",
                column: "TherapeuticGroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Medicines_TherapeuticGroupId",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "IsSection100",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "MaxQuantity",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "MaxRepeats",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "Route",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "TherapeuticGroupId",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "TherapeuticGroupTitle",
                table: "Medicines");
        }
    }
}
