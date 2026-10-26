using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class RestaurantDbIniCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Admin",
                columns: table => new
                {
                    adminID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    adminName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    adminEmail = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    adminPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    adminCreateAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admin", x => x.adminID);
                });

            migrationBuilder.CreateTable(
                name: "Customer",
                columns: table => new
                {
                    custID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    custName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    custEmail = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    custPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    custPhone = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    custCreateAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DietryPref = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customer", x => x.custID);
                });

            migrationBuilder.CreateTable(
                name: "Menus",
                columns: table => new
                {
                    menuID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    menuName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    menuDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    menuCategory = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    menuPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    isAvailable = table.Column<bool>(type: "bit", nullable: false),
                    menuImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menus", x => x.menuID);
                });

            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    staffID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    staffName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    staffEmail = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    staffPassword = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    staffPhone = table.Column<string>(type: "nvarchar(11)", maxLength: 11, nullable: false),
                    staffRole = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    staffActive = table.Column<bool>(type: "bit", nullable: false),
                    staffCreateAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.staffID);
                });

            migrationBuilder.CreateTable(
                name: "Tables",
                columns: table => new
                {
                    tableID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    tableNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    tableCapacity = table.Column<int>(type: "int", nullable: false),
                    diningArea = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    tableStatus = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tables", x => x.tableID);
                });

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    announcementID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    adminID = table.Column<int>(type: "int", nullable: false),
                    announcementTitle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    announcementMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    targetAudience = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    isPinned = table.Column<bool>(type: "bit", nullable: false),
                    announcementDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.announcementID);
                    table.ForeignKey(
                        name: "FK_Announcements_Admin_adminID",
                        column: x => x.adminID,
                        principalTable: "Admin",
                        principalColumn: "adminID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Promotions",
                columns: table => new
                {
                    promoID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    adminID = table.Column<int>(type: "int", nullable: false),
                    promoTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    promoDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    promoLabel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    promoCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    discountType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    discountValue = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    autoApply = table.Column<bool>(type: "bit", nullable: false),
                    startDay = table.Column<int>(type: "int", nullable: false),
                    endDay = table.Column<int>(type: "int", nullable: false),
                    validFrom = table.Column<DateTime>(type: "datetime2", nullable: true),
                    validUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    promoActive = table.Column<bool>(type: "bit", nullable: false),
                    promoCreateAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Promotions", x => x.promoID);
                    table.ForeignKey(
                        name: "FK_Promotions_Admin_adminID",
                        column: x => x.adminID,
                        principalTable: "Admin",
                        principalColumn: "adminID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reservations",
                columns: table => new
                {
                    reservationID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    custID = table.Column<int>(type: "int", nullable: false),
                    tableID = table.Column<int>(type: "int", nullable: false),
                    staffID = table.Column<int>(type: "int", nullable: true),
                    reservationDateTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    partySize = table.Column<int>(type: "int", nullable: false),
                    specialRequest = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    reservationStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    reservationCreateAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reservations", x => x.reservationID);
                    table.ForeignKey(
                        name: "FK_Reservations_Customer_custID",
                        column: x => x.custID,
                        principalTable: "Customer",
                        principalColumn: "custID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reservations_Staff_staffID",
                        column: x => x.staffID,
                        principalTable: "Staff",
                        principalColumn: "staffID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Reservations_Tables_tableID",
                        column: x => x.tableID,
                        principalTable: "Tables",
                        principalColumn: "tableID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    orderID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    custID = table.Column<int>(type: "int", nullable: false),
                    reservationID = table.Column<int>(type: "int", nullable: true),
                    staffID = table.Column<int>(type: "int", nullable: true),
                    orderDateTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    orderStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    totalAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.orderID);
                    table.ForeignKey(
                        name: "FK_Orders_Customer_custID",
                        column: x => x.custID,
                        principalTable: "Customer",
                        principalColumn: "custID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_Reservations_reservationID",
                        column: x => x.reservationID,
                        principalTable: "Reservations",
                        principalColumn: "reservationID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_Staff_staffID",
                        column: x => x.staffID,
                        principalTable: "Staff",
                        principalColumn: "staffID",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    feedbackID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    custID = table.Column<int>(type: "int", nullable: false),
                    orderID = table.Column<int>(type: "int", nullable: false),
                    rating = table.Column<int>(type: "int", nullable: false),
                    comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    feedbackDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.feedbackID);
                    table.ForeignKey(
                        name: "FK_Feedbacks_Customer_custID",
                        column: x => x.custID,
                        principalTable: "Customer",
                        principalColumn: "custID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Feedbacks_Orders_orderID",
                        column: x => x.orderID,
                        principalTable: "Orders",
                        principalColumn: "orderID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    orderItemID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    orderID = table.Column<int>(type: "int", nullable: false),
                    menuID = table.Column<int>(type: "int", nullable: false),
                    quantity = table.Column<int>(type: "int", nullable: false),
                    unitPrice = table.Column<decimal>(type: "decimal(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.orderItemID);
                    table.ForeignKey(
                        name: "FK_OrderItems_Menus_menuID",
                        column: x => x.menuID,
                        principalTable: "Menus",
                        principalColumn: "menuID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_orderID",
                        column: x => x.orderID,
                        principalTable: "Orders",
                        principalColumn: "orderID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    paymentID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    orderID = table.Column<int>(type: "int", nullable: false),
                    paymentAmount = table.Column<decimal>(type: "decimal(10,2)", nullable: false),
                    paymentMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    paymentStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    paymentDate = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.paymentID);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_orderID",
                        column: x => x.orderID,
                        principalTable: "Orders",
                        principalColumn: "orderID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admin_adminEmail",
                table: "Admin",
                column: "adminEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_adminID",
                table: "Announcements",
                column: "adminID");

            migrationBuilder.CreateIndex(
                name: "IX_Customer_custEmail",
                table: "Customer",
                column: "custEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_custID",
                table: "Feedbacks",
                column: "custID");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_orderID",
                table: "Feedbacks",
                column: "orderID");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_menuID",
                table: "OrderItems",
                column: "menuID");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_orderID",
                table: "OrderItems",
                column: "orderID");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_custID",
                table: "Orders",
                column: "custID");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_reservationID",
                table: "Orders",
                column: "reservationID");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_staffID",
                table: "Orders",
                column: "staffID");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_orderID",
                table: "Payments",
                column: "orderID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_adminID",
                table: "Promotions",
                column: "adminID");

            migrationBuilder.CreateIndex(
                name: "IX_Promotions_promoCode",
                table: "Promotions",
                column: "promoCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_custID",
                table: "Reservations",
                column: "custID");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_staffID",
                table: "Reservations",
                column: "staffID");

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_tableID",
                table: "Reservations",
                column: "tableID");

            migrationBuilder.CreateIndex(
                name: "IX_Staff_staffEmail",
                table: "Staff",
                column: "staffEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "Feedbacks");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Promotions");

            migrationBuilder.DropTable(
                name: "Menus");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Admin");

            migrationBuilder.DropTable(
                name: "Reservations");

            migrationBuilder.DropTable(
                name: "Customer");

            migrationBuilder.DropTable(
                name: "Staff");

            migrationBuilder.DropTable(
                name: "Tables");
        }
    }
}
