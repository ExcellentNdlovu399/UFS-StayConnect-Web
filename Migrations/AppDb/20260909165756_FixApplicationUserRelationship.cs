using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class FixApplicationUserRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_ApplicationUser_UserID",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_ApplicationUser_UserID",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_ApplicationUser_UserID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_ApplicationUser_UserID",
                table: "Reviews");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_AspNetUsers_UserID",
                table: "Applications",
                column: "UserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_AspNetUsers_UserID",
                table: "Notifications",
                column: "UserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_AspNetUsers_UserID",
                table: "Payments",
                column: "UserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_AspNetUsers_UserID",
                table: "Reviews",
                column: "UserID",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Applications_AspNetUsers_UserID",
                table: "Applications");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_AspNetUsers_UserID",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_AspNetUsers_UserID",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Reviews_AspNetUsers_UserID",
                table: "Reviews");

            migrationBuilder.AddForeignKey(
                name: "FK_Applications_ApplicationUser_UserID",
                table: "Applications",
                column: "UserID",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_ApplicationUser_UserID",
                table: "Notifications",
                column: "UserID",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_ApplicationUser_UserID",
                table: "Payments",
                column: "UserID",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Reviews_ApplicationUser_UserID",
                table: "Reviews",
                column: "UserID",
                principalTable: "ApplicationUser",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
