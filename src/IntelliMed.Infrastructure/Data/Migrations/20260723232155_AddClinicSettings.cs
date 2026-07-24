using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClinicSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClinicSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PracticeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Abn = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Fax = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Website = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    Suburb = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Postcode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ClinicSettings",
                columns: new[] { "Id", "Abn", "Address", "Email", "Fax", "Phone", "Postcode", "PracticeName", "State", "Suburb", "Website" },
                values: new object[] { 1, null, null, null, null, null, null, "IntelliMed Clinic", null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClinicSettings");
        }
    }
}
