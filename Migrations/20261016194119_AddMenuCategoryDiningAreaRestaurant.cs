using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuCategoryDiningAreaRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "diningArea",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "menuCategory",
                table: "Menus");

            migrationBuilder.AddColumn<int>(
                name: "areaID",
                table: "Tables",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "categoryID",
                table: "Menus",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DiningAreas",
                columns: table => new
                {
                    areaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    areaName = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningAreas", x => x.areaID);
                });

            migrationBuilder.CreateTable(
                name: "MenuCategories",
                columns: table => new
                {
                    categoryID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    categoryName = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategories", x => x.categoryID);
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                columns: table => new
                {
                    restaurantID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    restaurantName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    restaurantAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    restaurantPhone = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.restaurantID);
                });

            migrationBuilder.CreateTable(
                name: "OperatingHours",
                columns: table => new
                {
                    hourID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    restaurantID = table.Column<int>(type: "int", nullable: false),
                    dayOfWeek = table.Column<int>(type: "int", nullable: false),
                    openTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    closeTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    isClosed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperatingHours", x => x.hourID);
                    table.ForeignKey(
                        name: "FK_OperatingHours_Restaurants_restaurantID",
                        column: x => x.restaurantID,
                        principalTable: "Restaurants",
                        principalColumn: "restaurantID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tables_areaID",
                table: "Tables",
                column: "areaID");

            migrationBuilder.CreateIndex(
                name: "IX_Menus_categoryID",
                table: "Menus",
                column: "categoryID");

            migrationBuilder.CreateIndex(
                name: "IX_DiningAreas_areaName",
                table: "DiningAreas",
                column: "areaName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_categoryName",
                table: "MenuCategories",
                column: "categoryName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperatingHours_restaurantID_dayOfWeek",
                table: "OperatingHours",
                columns: new[] { "restaurantID", "dayOfWeek" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Menus_MenuCategories_categoryID",
                table: "Menus",
                column: "categoryID",
                principalTable: "MenuCategories",
                principalColumn: "categoryID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Tables_DiningAreas_areaID",
                table: "Tables",
                column: "areaID",
                principalTable: "DiningAreas",
                principalColumn: "areaID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Menus_MenuCategories_categoryID",
                table: "Menus");

            migrationBuilder.DropForeignKey(
                name: "FK_Tables_DiningAreas_areaID",
                table: "Tables");

            migrationBuilder.DropTable(
                name: "DiningAreas");

            migrationBuilder.DropTable(
                name: "MenuCategories");

            migrationBuilder.DropTable(
                name: "OperatingHours");

            migrationBuilder.DropTable(
                name: "Restaurants");

            migrationBuilder.DropIndex(
                name: "IX_Tables_areaID",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Menus_categoryID",
                table: "Menus");

            migrationBuilder.DropColumn(
                name: "areaID",
                table: "Tables");

            migrationBuilder.DropColumn(
                name: "categoryID",
                table: "Menus");

            migrationBuilder.AddColumn<string>(
                name: "diningArea",
                table: "Tables",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "menuCategory",
                table: "Menus",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
