using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicineRpbsAndRestriction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRpbsListed",
                table: "Medicines",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RestrictionLevel",
                table: "Medicines",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRpbsListed",
                table: "Medicines");

            migrationBuilder.DropColumn(
                name: "RestrictionLevel",
                table: "Medicines");
        }
    }
}
