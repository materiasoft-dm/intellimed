using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PatientTypeAndHealthFund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HealthFundCode",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HealthFundName",
                table: "Patients");

            migrationBuilder.AddColumn<DateTime>(
                name: "HealthFundJoinDate",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HealthFundId",
                table: "Patients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Patients",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "HealthFunds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthFunds", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "HealthFunds",
                columns: new[] { "Id", "Code", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, "MED", true, "Medibank Private" },
                    { 2, "BUP", true, "Bupa" },
                    { 3, "HCF", true, "HCF" },
                    { 4, "NIB", true, "nib" },
                    { 5, "GMH", true, "GMHBA" },
                    { 6, "AU", true, "Australian Unity" },
                    { 7, "HBF", true, "HBF Health" },
                    { 8, "TUH", true, "Teachers Health" },
                    { 9, "DHF", true, "Doctors' Health Fund" },
                    { 10, "FRK", true, "Frank Health Insurance" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_HealthFundId",
                table: "Patients",
                column: "HealthFundId");

            migrationBuilder.CreateIndex(
                name: "IX_HealthFunds_Code",
                table: "HealthFunds",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_HealthFunds_HealthFundId",
                table: "Patients",
                column: "HealthFundId",
                principalTable: "HealthFunds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Patients_HealthFunds_HealthFundId",
                table: "Patients");

            migrationBuilder.DropTable(
                name: "HealthFunds");

            migrationBuilder.DropIndex(
                name: "IX_Patients_HealthFundId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HealthFundId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "HealthFundJoinDate",
                table: "Patients");

            migrationBuilder.AddColumn<string>(
                name: "HealthFundName",
                table: "Patients",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HealthFundCode",
                table: "Patients",
                type: "TEXT",
                maxLength: 20,
                nullable: true);
        }
    }
}
