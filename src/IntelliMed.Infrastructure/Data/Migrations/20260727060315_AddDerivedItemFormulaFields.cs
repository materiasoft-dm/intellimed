using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IntelliMed.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDerivedItemFormulaFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UnitValue",
                table: "DerivedItemConfigs",
                newName: "UnderNumOfLimitPatientPlusTotal");

            migrationBuilder.RenameColumn(
                name: "OverLimitUnitValue",
                table: "DerivedItemConfigs",
                newName: "PercentageFromInvoiceTotal");

            migrationBuilder.RenameColumn(
                name: "LimitQuantity",
                table: "DerivedItemConfigs",
                newName: "OverNumOfLimitPatientPlus");

            migrationBuilder.AddColumn<decimal>(
                name: "MedicalGapPercent",
                table: "FeeScheduleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverLimitQuantityPlus",
                table: "FeeScheduleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentageFromAssociatedItemFee",
                table: "FeeScheduleItems",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssociatedItemNumbers",
                table: "DerivedItemConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CalculateFromFee",
                table: "DerivedItemConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupAAssociatedItems",
                table: "DerivedItemConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupBAssociatedItems",
                table: "DerivedItemConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupCAssociatedItems",
                table: "DerivedItemConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GroupDAssociatedItems",
                table: "DerivedItemConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumOfLimitPatient",
                table: "DerivedItemConfigs",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "NumberOfLimitQuantity",
                table: "DerivedItemConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OverLimitQuantityPlus",
                table: "DerivedItemConfigs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DerivedItemRateCalculateds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FeeScheduleItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderNum = table.Column<int>(type: "INTEGER", nullable: false),
                    Fee = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DerivedItemRateCalculateds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DerivedItemRateCalculateds_FeeScheduleItems_FeeScheduleItemId",
                        column: x => x.FeeScheduleItemId,
                        principalTable: "FeeScheduleItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DerivedItemRateCalculateds_FeeScheduleItemId_OrderNum",
                table: "DerivedItemRateCalculateds",
                columns: new[] { "FeeScheduleItemId", "OrderNum" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DerivedItemRateCalculateds");

            migrationBuilder.DropColumn(
                name: "MedicalGapPercent",
                table: "FeeScheduleItems");

            migrationBuilder.DropColumn(
                name: "OverLimitQuantityPlus",
                table: "FeeScheduleItems");

            migrationBuilder.DropColumn(
                name: "PercentageFromAssociatedItemFee",
                table: "FeeScheduleItems");

            migrationBuilder.DropColumn(
                name: "AssociatedItemNumbers",
                table: "DerivedItemConfigs");

            migrationBuilder.DropColumn(
                name: "CalculateFromFee",
                table: "DerivedItemConfigs");

            migrationBuilder.DropColumn(
                name: "GroupAAssociatedItems",
                table: "DerivedItemConfigs");

            migrationBuilder.DropColumn(
                name: "GroupBAssociatedItems",
                table: "DerivedItemConfigs");

            migrationBuilder.DropColumn(
                name: "GroupCAssociatedItems",
                table: "DerivedItemConfigs");

            migrationBuilder.DropColumn(
                name: "GroupDAssociatedItems",
                table: "DerivedItemConfigs");

            migrationBuilder.DropColumn(
                name: "NumOfLimitPatient",
                table: "DerivedItemConfigs");

            migrationBuilder.DropColumn(
                name: "NumberOfLimitQuantity",
                table: "DerivedItemConfigs");

            migrationBuilder.DropColumn(
                name: "OverLimitQuantityPlus",
                table: "DerivedItemConfigs");

            migrationBuilder.RenameColumn(
                name: "UnderNumOfLimitPatientPlusTotal",
                table: "DerivedItemConfigs",
                newName: "UnitValue");

            migrationBuilder.RenameColumn(
                name: "PercentageFromInvoiceTotal",
                table: "DerivedItemConfigs",
                newName: "OverLimitUnitValue");

            migrationBuilder.RenameColumn(
                name: "OverNumOfLimitPatientPlus",
                table: "DerivedItemConfigs",
                newName: "LimitQuantity");
        }
    }
}
