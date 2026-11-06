using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    // The seeded EARLY15 promotion says "15% off before 18:30" but had no time window, so it applied all evening.
    // Gives it 12:00 – 18:30, only if an admin hasn't set a time window already.
    public partial class EarlyDinnerHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE Promotions SET startTime = '12:00', endTime = '18:30'
WHERE promoCode = 'EARLY15' AND startTime IS NULL AND endTime IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE Promotions SET startTime = NULL, endTime = NULL
WHERE promoCode = 'EARLY15' AND startTime = '12:00' AND endTime = '18:30';");
        }
    }
}
