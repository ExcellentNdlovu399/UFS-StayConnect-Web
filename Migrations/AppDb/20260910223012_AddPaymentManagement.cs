using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddPaymentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ReferenceNumber",
                table: "Payments");

            migrationBuilder.RenameColumn(
                name: "TypeName",
                table: "PaymentTypes",
                newName: "Name");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "PaymentTypes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "PaymentTypes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "PaymentTypes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNumber",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Payments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AddColumn<int>(
                name: "ApplicationID",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Payments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ApplicationID",
                table: "Payments",
                column: "ApplicationID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReferenceNumber",
                table: "Payments",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Applications_ApplicationID",
                table: "Payments",
                column: "ApplicationID",
                principalTable: "Applications",
                principalColumn: "ApplicationID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Applications_ApplicationID",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ApplicationID",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReferenceNumber",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "PaymentTypes");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "PaymentTypes",
                newName: "TypeName");

            migrationBuilder.DropColumn(
                name: "ApplicationID",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Payments");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "PaymentTypes",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TypeName",
                table: "PaymentTypes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceNumber",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "PaymentStatus",
                table: "Payments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReferenceNumber",
                table: "Payments",
                column: "ReferenceNumber",
                unique: true,
                filter: "[ReferenceNumber] IS NOT NULL");
        }
    }
}
