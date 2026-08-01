using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptingModelAndBackfillPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Receipts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClinicId = table.Column<int>(type: "INTEGER", nullable: false),
                    PayerClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    ReceiptDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LegacyGuid = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_Clients_PayerClientId",
                        column: x => x.PayerClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Receipts_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceiptId = table.Column<int>(type: "INTEGER", nullable: false),
                    InvoiceId = table.Column<int>(type: "INTEGER", nullable: true),
                    InvoiceItemId = table.Column<int>(type: "INTEGER", nullable: true),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    AllocationType = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LegacyGuid = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptAllocations_InvoiceItems_InvoiceItemId",
                        column: x => x.InvoiceItemId,
                        principalTable: "InvoiceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptAllocations_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptAllocations_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ReceiptId = table.Column<int>(type: "INTEGER", nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LegacyGuid = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptPayments_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAllocations_InvoiceId",
                table: "ReceiptAllocations",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAllocations_InvoiceItemId",
                table: "ReceiptAllocations",
                column: "InvoiceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAllocations_LegacyGuid",
                table: "ReceiptAllocations",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptAllocations_ReceiptId",
                table: "ReceiptAllocations",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptPayments_LegacyGuid",
                table: "ReceiptPayments",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptPayments_ReceiptId",
                table: "ReceiptPayments",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_ClinicId",
                table: "Receipts",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_LegacyGuid",
                table: "Receipts",
                column: "LegacyGuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_PayerClientId",
                table: "Receipts",
                column: "PayerClientId");

            // Backfill: one synthetic Receipt per existing Payment row, not an attempt to regroup old
            // payments back into their original multi-tender receipts — the legacy ReceiptGUID needed
            // for that was only ever used as a transient in-memory key during the original import and
            // was never persisted anywhere, so it genuinely isn't recoverable. Each backfilled
            // allocation is invoice-level (InvoiceItemId left null — no fabricated line-item
            // precision), cleanly distinct from the payer-credit meaning (both InvoiceId and
            // InvoiceItemId null) since backfill never produces credit rows.
            //
            // Correlation key: COALESCE(Payment.LegacyGuid, 'legacy-payment:' || Payment.Id). Imported
            // rows already carry a unique LegacyGuid; native rows (e.g. from the "+ Record Payment"
            // modal, before this migration) get a synthesized one, since NULL = NULL never joins.
            migrationBuilder.Sql(@"
                INSERT INTO Receipts (ClinicId, PayerClientId, ReceiptDate, CreatedAt, LegacyGuid)
                SELECT i.ClinicId, COALESCE(c.PayerClientId, c.Id), p.PaymentDate, p.CreatedAt,
                       COALESCE(p.LegacyGuid, 'legacy-payment:' || p.Id)
                FROM Payments p
                JOIN Invoices i ON i.Id = p.InvoiceId
                JOIN Clients c ON c.Id = i.ClientId;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ReceiptPayments (ReceiptId, Amount, Method, Reference, CreatedAt, LegacyGuid)
                SELECT r.Id, p.Amount, p.Method, p.Reference, p.CreatedAt,
                       COALESCE(p.LegacyGuid, 'legacy-payment:' || p.Id)
                FROM Payments p
                JOIN Receipts r ON r.LegacyGuid = COALESCE(p.LegacyGuid, 'legacy-payment:' || p.Id);
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ReceiptAllocations (ReceiptId, InvoiceId, InvoiceItemId, Amount, AllocationType, CreatedAt, LegacyGuid)
                SELECT r.Id, p.InvoiceId, NULL, p.Amount, 0, p.CreatedAt,
                       COALESCE(p.LegacyGuid, 'legacy-payment:' || p.Id)
                FROM Payments p
                JOIN Receipts r ON r.LegacyGuid = COALESCE(p.LegacyGuid, 'legacy-payment:' || p.Id);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReceiptAllocations");

            migrationBuilder.DropTable(
                name: "ReceiptPayments");

            migrationBuilder.DropTable(
                name: "Receipts");
        }
    }
}
