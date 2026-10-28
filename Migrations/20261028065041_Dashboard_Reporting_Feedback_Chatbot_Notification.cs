using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class Dashboard_Reporting_Feedback_Chatbot_Notification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_custID",
                table: "Feedbacks");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    notificationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    custID = table.Column<int>(type: "int", nullable: false),
                    notificationCategory = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    notificationTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    notificationMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    linkUrl = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    isRead = table.Column<bool>(type: "bit", nullable: false),
                    notificationCreateAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.notificationID);
                    table.ForeignKey(
                        name: "FK_Notifications_Customer_custID",
                        column: x => x.custID,
                        principalTable: "Customer",
                        principalColumn: "custID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_custID_orderID",
                table: "Feedbacks",
                columns: new[] { "custID", "orderID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_custID_isRead",
                table: "Notifications",
                columns: new[] { "custID", "isRead" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Feedbacks_custID_orderID",
                table: "Feedbacks");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_custID",
                table: "Feedbacks",
                column: "custID");
        }
    }
}
