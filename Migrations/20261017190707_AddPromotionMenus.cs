using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuPromotion",
                columns: table => new
                {
                    MenusmenuID = table.Column<int>(type: "int", nullable: false),
                    PromotionspromoID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuPromotion", x => new { x.MenusmenuID, x.PromotionspromoID });
                    table.ForeignKey(
                        name: "FK_MenuPromotion_Menus_MenusmenuID",
                        column: x => x.MenusmenuID,
                        principalTable: "Menus",
                        principalColumn: "menuID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuPromotion_Promotions_PromotionspromoID",
                        column: x => x.PromotionspromoID,
                        principalTable: "Promotions",
                        principalColumn: "promoID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuPromotion_PromotionspromoID",
                table: "MenuPromotion",
                column: "PromotionspromoID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuPromotion");
        }
    }
}
