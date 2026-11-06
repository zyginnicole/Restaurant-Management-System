using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class SyncCurrentModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Promotions_promoID",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_Tables_areaID",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_tableNumber",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_promoID",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_DiningAreas_areaCode",
                table: "DiningAreas");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_areaID_tableNumber",
                table: "Tables",
                columns: new[] { "areaID", "tableNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tables_areaID_tableNumber",
                table: "Tables");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_areaID",
                table: "Tables",
                column: "areaID");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_tableNumber",
                table: "Tables",
                column: "tableNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_promoID",
                table: "OrderItems",
                column: "promoID");

            migrationBuilder.CreateIndex(
                name: "IX_DiningAreas_areaCode",
                table: "DiningAreas",
                column: "areaCode",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Promotions_promoID",
                table: "OrderItems",
                column: "promoID",
                principalTable: "Promotions",
                principalColumn: "promoID",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
