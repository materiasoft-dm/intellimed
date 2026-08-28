using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionWizardFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EpisodeId",
                table: "Prescriptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Instruction",
                table: "Prescriptions",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLimitedDuration",
                table: "Prescriptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "Prescriptions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "Prescriptions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_EpisodeId",
                table: "Prescriptions",
                column: "EpisodeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_Episodes_EpisodeId",
                table: "Prescriptions",
                column: "EpisodeId",
                principalTable: "Episodes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_Episodes_EpisodeId",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_EpisodeId",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "EpisodeId",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "Instruction",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "IsLimitedDuration",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "Prescriptions");
        }
    }
}
