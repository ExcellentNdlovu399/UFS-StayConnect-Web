using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddPaymentDueDatesAndReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "Payments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReminderKey",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "AccommodationCharges",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReminderDaysAfterDue",
                table: "AccommodationCharges",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ReminderDaysBeforeDue",
                table: "AccommodationCharges",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReminderKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "AccommodationCharges");

            migrationBuilder.DropColumn(
                name: "ReminderDaysAfterDue",
                table: "AccommodationCharges");

            migrationBuilder.DropColumn(
                name: "ReminderDaysBeforeDue",
                table: "AccommodationCharges");
        }
    }
}
