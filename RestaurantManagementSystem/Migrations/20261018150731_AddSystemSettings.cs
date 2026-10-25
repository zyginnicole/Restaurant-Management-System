using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    settingID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    restaurantName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    tagline = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    timeZone = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    diningMinutes = table.Column<int>(type: "int", nullable: false),
                    minPartySize = table.Column<int>(type: "int", nullable: false),
                    maxPartySize = table.Column<int>(type: "int", nullable: false),
                    bookingWindowDays = table.Column<int>(type: "int", nullable: false),
                    minNoticeHours = table.Column<int>(type: "int", nullable: false),
                    cancelWindowHours = table.Column<int>(type: "int", nullable: false),
                    lateHoldMinutes = table.Column<int>(type: "int", nullable: false),
                    autoConfirm = table.Column<bool>(type: "bit", nullable: false),
                    serviceChargePct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    sstPct = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    receiptFooter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    acceptCash = table.Column<bool>(type: "bit", nullable: false),
                    acceptCard = table.Column<bool>(type: "bit", nullable: false),
                    acceptFpx = table.Column<bool>(type: "bit", nullable: false),
                    acceptEwallet = table.Column<bool>(type: "bit", nullable: false),
                    passwordMinLength = table.Column<int>(type: "int", nullable: false),
                    requireUpper = table.Column<bool>(type: "bit", nullable: false),
                    requireNumber = table.Column<bool>(type: "bit", nullable: false),
                    requireSymbol = table.Column<bool>(type: "bit", nullable: false),
                    maxFailedLogins = table.Column<int>(type: "int", nullable: false),
                    idleTimeoutMinutes = table.Column<int>(type: "int", nullable: false),
                    requireTwoStep = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.settingID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemSettings");
        }
    }
}
