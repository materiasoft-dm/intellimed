using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Prescriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    MedicineId = table.Column<int>(type: "INTEGER", nullable: true),
                    MedicineName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Dose = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Frequency = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Route = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Quantity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    DurationDays = table.Column<int>(type: "INTEGER", nullable: true),
                    Repeats = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeatIntervalDays = table.Column<int>(type: "INTEGER", nullable: true),
                    ScriptNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsPbsListed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsRpbsListed = table.Column<bool>(type: "INTEGER", nullable: false),
                    RestrictionLevel = table.Column<int>(type: "INTEGER", nullable: true),
                    Schedule = table.Column<int>(type: "INTEGER", nullable: true),
                    AuthorityApprovalNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    StateAuthorityCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    PrescribingNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    NotesForPharmacy = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AppointmentId = table.Column<int>(type: "INTEGER", nullable: true),
                    PrescribedByPractitionerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SafetyOverrideAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false),
                    SafetyOverrideNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CeasedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Medicines_MedicineId",
                        column: x => x.MedicineId,
                        principalTable: "Medicines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Practitioners_PrescribedByPractitionerId",
                        column: x => x.PrescribedByPractitionerId,
                        principalTable: "Practitioners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrescriptionId = table.Column<int>(type: "INTEGER", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IssuedByPractitionerId = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeatNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    DeliveryMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    TransmissionStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    Dose = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Frequency = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Route = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Quantity = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Repeats = table.Column<int>(type: "INTEGER", nullable: false),
                    PrescribingNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    NotesForPharmacy = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    PrescriberName = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    PrescriberProviderNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PrescriberAhpraNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PrescriberHpiiNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PrescriberQualifications = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionIssues_Practitioners_IssuedByPractitionerId",
                        column: x => x.IssuedByPractitionerId,
                        principalTable: "Practitioners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PrescriptionIssues_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionIssues_IssuedByPractitionerId",
                table: "PrescriptionIssues",
                column: "IssuedByPractitionerId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionIssues_PrescriptionId",
                table: "PrescriptionIssues",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_AppointmentId",
                table: "Prescriptions",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ClientId",
                table: "Prescriptions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_MedicineId",
                table: "Prescriptions",
                column: "MedicineId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PrescribedByPractitionerId",
                table: "Prescriptions",
                column: "PrescribedByPractitionerId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ScriptNumber",
                table: "Prescriptions",
                column: "ScriptNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrescriptionIssues");

            migrationBuilder.DropTable(
                name: "Prescriptions");
        }
    }
}
