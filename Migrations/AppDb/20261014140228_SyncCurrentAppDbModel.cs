using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class SyncCurrentAppDbModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "RatePerUnit",
                table: "Services",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ProofOfPaymentPath",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MoveInDate",
                table: "Applications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfVehicles",
                table: "Applications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ParkingFee",
                table: "Applications",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RatePerUnit",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "ProofOfPaymentPath",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "MoveInDate",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "NumberOfVehicles",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "ParkingFee",
                table: "Applications");
        }
    }
}
