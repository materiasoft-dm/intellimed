using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentTimeslotSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumTimeslotMinutes",
                table: "ClinicSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DefaultAppointmentTimeslotMinutes",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "ClinicSettings",
                keyColumn: "Id",
                keyValue: 1,
                column: "MinimumTimeslotMinutes",
                value: 15);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumTimeslotMinutes",
                table: "ClinicSettings");

            migrationBuilder.DropColumn(
                name: "DefaultAppointmentTimeslotMinutes",
                table: "AspNetUsers");
        }
    }
}
