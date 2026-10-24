using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResultsInbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientLabIdentifiers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    SenderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Identifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientLabIdentifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientLabIdentifiers_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResultIngestKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClinicId = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    KeyPrefix = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    KeyHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultIngestKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultIngestKeys_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClinicId = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    AssignedPractitionerId = table.Column<int>(type: "INTEGER", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SenderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MessageControlId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PatientIdentifier = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PatientFamilyName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PatientGivenName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PatientDateOfBirth = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PatientSex = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    PatientMedicareNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    OrderingProviderName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OrderingProviderNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    PlacerOrderNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FillerOrderNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReportName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HasAbnormal = table.Column<bool>(type: "INTEGER", nullable: false),
                    RawMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReviewComment = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MatchedByUserId = table.Column<string>(type: "TEXT", nullable: true),
                    MatchedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    HoldNote = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultReports_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResultReports_Practitioners_AssignedPractitionerId",
                        column: x => x.AssignedPractitionerId,
                        principalTable: "Practitioners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResultAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResultReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultAttachments_ResultReports_ResultReportId",
                        column: x => x.ResultReportId,
                        principalTable: "ResultReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResultItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ResultReportId = table.Column<int>(type: "INTEGER", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true),
                    Units = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ReferenceRange = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AbnormalFlag = table.Column<string>(type: "TEXT", maxLength: 10, nullable: true),
                    IsAbnormal = table.Column<bool>(type: "INTEGER", nullable: false),
                    ResultStatus = table.Column<string>(type: "TEXT", maxLength: 5, nullable: true),
                    Comment = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResultItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResultItems_ResultReports_ResultReportId",
                        column: x => x.ResultReportId,
                        principalTable: "ResultReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientLabIdentifiers_ClientId_SenderName_Identifier",
                table: "ClientLabIdentifiers",
                columns: new[] { "ClientId", "SenderName", "Identifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientLabIdentifiers_SenderName_Identifier",
                table: "ClientLabIdentifiers",
                columns: new[] { "SenderName", "Identifier" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultAttachments_ResultReportId",
                table: "ResultAttachments",
                column: "ResultReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultIngestKeys_ClinicId",
                table: "ResultIngestKeys",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultIngestKeys_KeyHash",
                table: "ResultIngestKeys",
                column: "KeyHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultItems_ResultReportId",
                table: "ResultItems",
                column: "ResultReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultReports_AssignedPractitionerId",
                table: "ResultReports",
                column: "AssignedPractitionerId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultReports_ClientId",
                table: "ResultReports",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ResultReports_ClinicId_SenderName_MessageControlId",
                table: "ResultReports",
                columns: new[] { "ClinicId", "SenderName", "MessageControlId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResultReports_ClinicId_Status",
                table: "ResultReports",
                columns: new[] { "ClinicId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ResultReports_PlacerOrderNumber",
                table: "ResultReports",
                column: "PlacerOrderNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientLabIdentifiers");

            migrationBuilder.DropTable(
                name: "ResultAttachments");

            migrationBuilder.DropTable(
                name: "ResultIngestKeys");

            migrationBuilder.DropTable(
                name: "ResultItems");

            migrationBuilder.DropTable(
                name: "ResultReports");
        }
    }
}
