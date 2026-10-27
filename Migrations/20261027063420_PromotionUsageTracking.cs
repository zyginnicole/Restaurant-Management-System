using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class PromotionUsageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "promoID",
                table: "OrderItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_promoID",
                table: "OrderItems",
                column: "promoID");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItems_Promotions_promoID",
                table: "OrderItems",
                column: "promoID",
                principalTable: "Promotions",
                principalColumn: "promoID",
                onDelete: ReferentialAction.SetNull);

            // Existing order lines: link them to their promotion by code, so earlier uses are counted
            migrationBuilder.Sql(@"
                UPDATE oi SET oi.promoID = p.promoID
                FROM OrderItems oi
                JOIN Promotions p ON p.promoCode = oi.promoCode
                WHERE oi.promoCode IS NOT NULL AND oi.promoID IS NULL;");

            // The seeded BIRTHDAY promotion was saved without its rules: birthday month only, once per customer per year.
            // Only touched if an admin has not set any of these rules on it yet.
            migrationBuilder.Sql(@"
                UPDATE Promotions
                SET birthdayOnly = 1, maxUsesPerCustomer = 1, limitResetsYearly = 1
                WHERE promoCode = 'BIRTHDAY' AND birthdayOnly = 0
                  AND maxUsesPerCustomer IS NULL AND limitResetsYearly = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderItems_Promotions_promoID",
                table: "OrderItems");

            migrationBuilder.DropIndex(
                name: "IX_OrderItems_promoID",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "promoID",
                table: "OrderItems");
        }
    }
}
