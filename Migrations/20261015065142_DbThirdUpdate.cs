using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class DbThirdUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Customer_custID",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Customer_custID",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Customer_custID",
                table: "Reservations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Customer",
                table: "Customer");

            migrationBuilder.RenameTable(
                name: "Customer",
                newName: "Customers");

            migrationBuilder.RenameColumn(
                name: "staffRole",
                table: "Staff",
                newName: "staffPosition");

            migrationBuilder.RenameColumn(
                name: "staffActive",
                table: "Staff",
                newName: "isActive");

            migrationBuilder.RenameColumn(
                name: "promoActive",
                table: "Promotions",
                newName: "isActive");

            migrationBuilder.RenameColumn(
                name: "LockoutEnd",
                table: "Customers",
                newName: "lockoutEnd");

            migrationBuilder.RenameColumn(
                name: "FailedLoginAttempts",
                table: "Customers",
                newName: "failedLoginCount");

            migrationBuilder.RenameIndex(
                name: "IX_Customer_custEmail",
                table: "Customers",
                newName: "IX_Customers_custEmail");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Customers",
                table: "Customers",
                column: "custID");

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
                        name: "FK_Notifications_Customers_custID",
                        column: x => x.custID,
                        principalTable: "Customers",
                        principalColumn: "custID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_custID",
                table: "Notifications",
                column: "custID");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Customers_custID",
                table: "Feedbacks",
                column: "custID",
                principalTable: "Customers",
                principalColumn: "custID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Customers_custID",
                table: "Orders",
                column: "custID",
                principalTable: "Customers",
                principalColumn: "custID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Customers_custID",
                table: "Reservations",
                column: "custID",
                principalTable: "Customers",
                principalColumn: "custID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Customers_custID",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Customers_custID",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Customers_custID",
                table: "Reservations");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Customers",
                table: "Customers");

            migrationBuilder.RenameTable(
                name: "Customers",
                newName: "Customer");

            migrationBuilder.RenameColumn(
                name: "staffPosition",
                table: "Staff",
                newName: "staffRole");

            migrationBuilder.RenameColumn(
                name: "isActive",
                table: "Staff",
                newName: "staffActive");

            migrationBuilder.RenameColumn(
                name: "isActive",
                table: "Promotions",
                newName: "promoActive");

            migrationBuilder.RenameColumn(
                name: "lockoutEnd",
                table: "Customer",
                newName: "LockoutEnd");

            migrationBuilder.RenameColumn(
                name: "failedLoginCount",
                table: "Customer",
                newName: "FailedLoginAttempts");

            migrationBuilder.RenameIndex(
                name: "IX_Customers_custEmail",
                table: "Customer",
                newName: "IX_Customer_custEmail");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Customer",
                table: "Customer",
                column: "custID");

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Customer_custID",
                table: "Feedbacks",
                column: "custID",
                principalTable: "Customer",
                principalColumn: "custID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Customer_custID",
                table: "Orders",
                column: "custID",
                principalTable: "Customer",
                principalColumn: "custID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Customer_custID",
                table: "Reservations",
                column: "custID",
                principalTable: "Customer",
                principalColumn: "custID",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
