using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicationUserId",
                table: "Practitioners",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedByPractitionerId",
                table: "Clients",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisibleToAllProviders",
                table: "Clients",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "ClientProviderAccesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    PractitionerId = table.Column<int>(type: "INTEGER", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientProviderAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientProviderAccesses_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClientProviderAccesses_Practitioners_PractitionerId",
                        column: x => x.PractitionerId,
                        principalTable: "Practitioners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Practitioners_ApplicationUserId",
                table: "Practitioners",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CreatedByPractitionerId",
                table: "Clients",
                column: "CreatedByPractitionerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientProviderAccesses_ClientId_PractitionerId",
                table: "ClientProviderAccesses",
                columns: new[] { "ClientId", "PractitionerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientProviderAccesses_PractitionerId",
                table: "ClientProviderAccesses",
                column: "PractitionerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Practitioners_CreatedByPractitionerId",
                table: "Clients",
                column: "CreatedByPractitionerId",
                principalTable: "Practitioners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Practitioners_CreatedByPractitionerId",
                table: "Clients");

            migrationBuilder.DropTable(
                name: "ClientProviderAccesses");

            migrationBuilder.DropIndex(
                name: "IX_Practitioners_ApplicationUserId",
                table: "Practitioners");

            migrationBuilder.DropIndex(
                name: "IX_Clients_CreatedByPractitionerId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "Practitioners");

            migrationBuilder.DropColumn(
                name: "CreatedByPractitionerId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "VisibleToAllProviders",
                table: "Clients");
        }
    }
}
