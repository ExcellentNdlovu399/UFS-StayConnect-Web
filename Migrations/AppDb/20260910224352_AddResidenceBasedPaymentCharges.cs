using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddResidenceBasedPaymentCharges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccommodationCharges",
                columns: table => new
                {
                    AccommodationChargeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AccommodationID = table.Column<int>(type: "int", nullable: false),
                    PaymentTypeID = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccommodationCharges", x => x.AccommodationChargeID);
                    table.ForeignKey(
                        name: "FK_AccommodationCharges_Accommodations_AccommodationID",
                        column: x => x.AccommodationID,
                        principalTable: "Accommodations",
                        principalColumn: "AccommodationID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AccommodationCharges_PaymentTypes_PaymentTypeID",
                        column: x => x.PaymentTypeID,
                        principalTable: "PaymentTypes",
                        principalColumn: "PaymentTypeID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationCharges_AccommodationID_PaymentTypeID",
                table: "AccommodationCharges",
                columns: new[] { "AccommodationID", "PaymentTypeID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccommodationCharges_PaymentTypeID",
                table: "AccommodationCharges",
                column: "PaymentTypeID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccommodationCharges");
        }
    }
}
