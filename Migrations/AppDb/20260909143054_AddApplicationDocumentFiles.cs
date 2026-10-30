using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddApplicationDocumentFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comments",
                table: "Applications");

            migrationBuilder.AddColumn<byte[]>(
                name: "FundingDocument",
                table: "Applications",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FundingDocumentName",
                table: "Applications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "IDDocument",
                table: "Applications",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IDDocumentName",
                table: "Applications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RegistrationDocument",
                table: "Applications",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationDocumentName",
                table: "Applications",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FundingDocument",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "FundingDocumentName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IDDocument",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "IDDocumentName",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "RegistrationDocument",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "RegistrationDocumentName",
                table: "Applications");

            migrationBuilder.AddColumn<string>(
                name: "Comments",
                table: "Applications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
