using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UFS_StayConnect.Migrations.AppDb
{
    /// <inheritdoc />
    public partial class AddPasswordAssistance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PasswordResetRequests",
                columns: table => new
                {
                    PasswordResetRequestID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProcessedByUserID = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetRequests", x => x.PasswordResetRequestID);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_AspNetUsers_ProcessedByUserID",
                        column: x => x.ProcessedByUserID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_AspNetUsers_UserID",
                        column: x => x.UserID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_ProcessedByUserID",
                table: "PasswordResetRequests",
                column: "ProcessedByUserID");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_UserID",
                table: "PasswordResetRequests",
                column: "UserID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PasswordResetRequests");
        }
    }
}
