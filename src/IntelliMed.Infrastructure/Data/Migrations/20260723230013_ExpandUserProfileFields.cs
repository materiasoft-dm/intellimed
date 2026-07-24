using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandUserProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "AspNetUsers",
                newName: "MobilePhone");

            migrationBuilder.AddColumn<string>(
                name: "BusinessHoursPhone",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EPrescribingEnabled",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Fax",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "InternalProvider",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalAddress",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalPostcode",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PostalSameAsResidential",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalState",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostalSuburb",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidentialAddress",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidentialPostcode",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidentialState",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResidentialSuburb",
                table: "AspNetUsers",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VocationallyRegistered",
                table: "AspNetUsers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ProviderGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderGroups", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ProviderGroups",
                columns: new[] { "Id", "IsActive", "Name" },
                values: new object[,]
                {
                    { 1, true, "General Practitioners" },
                    { 2, true, "Specialists" },
                    { 3, true, "Physiotherapists" },
                    { 4, true, "Dentists" },
                    { 5, true, "Nurse" },
                    { 6, true, "Allied Health Professional" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_GroupId",
                table: "AspNetUsers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderGroups_Name",
                table: "ProviderGroups",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_ProviderGroups_GroupId",
                table: "AspNetUsers",
                column: "GroupId",
                principalTable: "ProviderGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_ProviderGroups_GroupId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "ProviderGroups");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_GroupId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "BusinessHoursPhone",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EPrescribingEnabled",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Fax",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "InternalProvider",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostalAddress",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostalPostcode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostalSameAsResidential",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostalState",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PostalSuburb",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ResidentialAddress",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ResidentialPostcode",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ResidentialState",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ResidentialSuburb",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "VocationallyRegistered",
                table: "AspNetUsers");

            migrationBuilder.RenameColumn(
                name: "MobilePhone",
                table: "AspNetUsers",
                newName: "Phone");
        }
    }
}
