using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddAccommodationBedroomsBathrooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bathrooms",
                table: "Accommodations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Bedrooms",
                table: "Accommodations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bathrooms",
                table: "Accommodations");

            migrationBuilder.DropColumn(
                name: "Bedrooms",
                table: "Accommodations");
        }
    }
}
