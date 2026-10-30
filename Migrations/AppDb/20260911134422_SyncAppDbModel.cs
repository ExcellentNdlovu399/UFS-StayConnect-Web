using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class SyncAppDbModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccommodationChargeID",
                table: "Payments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentBatchReference",
                table: "Payments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Payments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RoomID",
                table: "Applications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    RoomID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccommodationID = table.Column<int>(type: "int", nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Floor = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    RoomType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.RoomID);
                    table.ForeignKey(
                        name: "FK_Rooms_Accommodations_AccommodationID",
                        column: x => x.AccommodationID,
                        principalTable: "Accommodations",
                        principalColumn: "AccommodationID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_AccommodationChargeID",
                table: "Payments",
                column: "AccommodationChargeID");

            migrationBuilder.CreateIndex(
                name: "IX_Applications_RoomID",
                table: "Applications",
                column: "RoomID");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_AccommodationID_RoomNumber",
                table: "Rooms",
                columns: new[] { "AccommodationID", "RoomNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_Rooms_RoomID",
                table: "Applications",
                column: "RoomID",
                principalTable: "Rooms",
                principalColumn: "RoomID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_AccommodationCharges_AccommodationChargeID",
                table: "Payments",
                column: "AccommodationChargeID",
                principalTable: "AccommodationCharges",
                principalColumn: "AccommodationChargeID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_Rooms_RoomID",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_AccommodationCharges_AccommodationChargeID",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_Payments_AccommodationChargeID",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Applications_RoomID",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "AccommodationChargeID",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentBatchReference",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RoomID",
                table: "Applications");
        }
    }
}
