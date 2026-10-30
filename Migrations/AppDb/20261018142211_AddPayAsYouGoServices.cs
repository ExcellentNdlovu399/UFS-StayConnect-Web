using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddPayAsYouGoServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChargingMethod",
                table: "Services",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasure",
                table: "Services",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "EmploymentDocumentName",
                table: "Applications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FreeUnits",
                table: "AccommodationServices",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "AccommodationServices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPayAsYouGo",
                table: "AccommodationServices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RatePerUnit",
                table: "AccommodationServices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UnitOfMeasure",
                table: "AccommodationServices",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ServiceUsageRecords",
                columns: table => new
                {
                    ServiceUsageRecordID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationID = table.Column<int>(type: "int", nullable: false),
                    ServiceID = table.Column<int>(type: "int", nullable: false),
                    ReadingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousReading = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CurrentReading = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    UnitsUsed = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    RatePerUnit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AmountDue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RecordedByUserID = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    BillingStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    BillingDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceUsageRecords", x => x.ServiceUsageRecordID);
                    table.ForeignKey(
                        name: "FK_ServiceUsageRecords_Applications_ApplicationID",
                        column: x => x.ApplicationID,
                        principalTable: "Applications",
                        principalColumn: "ApplicationID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ServiceUsageRecords_Services_ServiceID",
                        column: x => x.ServiceID,
                        principalTable: "Services",
                        principalColumn: "ServiceID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceUsageRecords_ApplicationID_ServiceID_ReadingDate",
                table: "ServiceUsageRecords",
                columns: new[] { "ApplicationID", "ServiceID", "ReadingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceUsageRecords_ServiceID",
                table: "ServiceUsageRecords",
                column: "ServiceID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceUsageRecords");

            migrationBuilder.DropColumn(
                name: "ChargingMethod",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "FreeUnits",
                table: "AccommodationServices");

            migrationBuilder.DropColumn(
                name: "IsEnabled",
                table: "AccommodationServices");

            migrationBuilder.DropColumn(
                name: "IsPayAsYouGo",
                table: "AccommodationServices");

            migrationBuilder.DropColumn(
                name: "RatePerUnit",
                table: "AccommodationServices");

            migrationBuilder.DropColumn(
                name: "UnitOfMeasure",
                table: "AccommodationServices");

            migrationBuilder.AlterColumn<string>(
                name: "EmploymentDocumentName",
                table: "Applications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
