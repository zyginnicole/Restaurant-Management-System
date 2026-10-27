using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AreaCodesForTableNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tables_areaID_tableNumber",
                table: "Tables");

            migrationBuilder.AddColumn<string>(
                name: "areaCode",
                table: "DiningAreas",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            // Existing areas get a code: the first letter, or the first 2 letters when another area already
            // has that letter, or letter + digit as a last resort (same order as TableNumbering.CodeFor).
            migrationBuilder.Sql(@"
UPDATE DiningAreas SET areaCode = UPPER(LEFT(LTRIM(areaName), 1));

WITH d AS (SELECT areaCode, areaName, ROW_NUMBER() OVER (PARTITION BY areaCode ORDER BY areaID) AS rn FROM DiningAreas)
UPDATE d SET areaCode = UPPER(LEFT(LTRIM(areaName), 2)) WHERE rn > 1;

WITH d AS (SELECT areaCode, areaName, areaID, ROW_NUMBER() OVER (PARTITION BY areaCode ORDER BY areaID) AS rn FROM DiningAreas)
UPDATE d SET areaCode = UPPER(LEFT(LTRIM(areaName), 1)) + CAST(rn AS nvarchar(2)) WHERE rn > 1;");

            // Existing tables are renumbered once inside their area, keeping their order:
            // Indoor T-01, T-02, T-03, T-07 -> I-01 ... I-04; Outdoor T-05, T-06, T-08 -> O-01 ... O-03; VIP V-01, V-02 stay.
            // Reservations and orders point to tableID, so they follow automatically.
            migrationBuilder.Sql(@"
WITH t AS (
    SELECT tb.tableNumber, a.areaCode,
           ROW_NUMBER() OVER (PARTITION BY tb.areaID
                              ORDER BY TRY_CAST(SUBSTRING(tb.tableNumber, PATINDEX('%[0-9]%', tb.tableNumber), 10) AS int), tb.tableID) AS rn
    FROM Tables tb JOIN DiningAreas a ON a.areaID = tb.areaID)
UPDATE t SET tableNumber = areaCode + '-' + RIGHT('0' + CAST(rn AS nvarchar(3)), 2);");

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
                name: "IX_DiningAreas_areaCode",
                table: "DiningAreas",
                column: "areaCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tables_areaID",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_Tables_tableNumber",
                table: "Tables");

            migrationBuilder.DropIndex(
                name: "IX_DiningAreas_areaCode",
                table: "DiningAreas");

            // back to the old T-xx style (numbers only need to be unique per area again)
            migrationBuilder.Sql(@"UPDATE Tables SET tableNumber = 'T-' + RIGHT(tableNumber, 2);");

            migrationBuilder.DropColumn(
                name: "areaCode",
                table: "DiningAreas");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_areaID_tableNumber",
                table: "Tables",
                columns: new[] { "areaID", "tableNumber" },
                unique: true);
        }
    }
}
