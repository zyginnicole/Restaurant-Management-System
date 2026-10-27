using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSlotsAndCategorySettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "lastSeatingMinutes",
                table: "Restaurants",
                type: "int",
                nullable: false,
                defaultValue: 60);

            migrationBuilder.AddColumn<int>(
                name: "slotIntervalMinutes",
                table: "Restaurants",
                type: "int",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<bool>(
                name: "categoryActive",
                table: "MenuCategories",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "categoryDescription",
                table: "MenuCategories",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BlockedSlots",
                columns: table => new
                {
                    blockedSlotID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    restaurantID = table.Column<int>(type: "int", nullable: false),
                    slotStart = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockedSlots", x => x.blockedSlotID);
                    table.ForeignKey(
                        name: "FK_BlockedSlots_Restaurants_restaurantID",
                        column: x => x.restaurantID,
                        principalTable: "Restaurants",
                        principalColumn: "restaurantID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockedSlots_restaurantID_slotStart",
                table: "BlockedSlots",
                columns: new[] { "restaurantID", "slotStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockedSlots");

            migrationBuilder.DropColumn(
                name: "lastSeatingMinutes",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "slotIntervalMinutes",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "categoryActive",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "categoryDescription",
                table: "MenuCategories");
        }
    }
}
