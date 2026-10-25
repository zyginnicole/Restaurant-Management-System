using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class OrdersTableAndRemarks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "pickupDateTime",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "tableID",
                table: "Orders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "orderRemark",
                table: "OrderItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_tableID",
                table: "Orders",
                column: "tableID");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Tables_tableID",
                table: "Orders",
                column: "tableID",
                principalTable: "Tables",
                principalColumn: "tableID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Tables_tableID",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_tableID",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "pickupDateTime",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "tableID",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "orderRemark",
                table: "OrderItems");
        }
    }
}
