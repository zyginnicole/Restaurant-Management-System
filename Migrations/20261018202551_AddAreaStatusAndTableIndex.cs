using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddAreaStatusAndTableIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tables_areaID",
                table: "Tables");

            migrationBuilder.AlterColumn<string>(
                name: "tableNumber",
                table: "Tables",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<bool>(
                name: "areaActive",
                table: "DiningAreas",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "areaDescription",
                table: "DiningAreas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

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

            migrationBuilder.DropColumn(
                name: "areaActive",
                table: "DiningAreas");

            migrationBuilder.DropColumn(
                name: "areaDescription",
                table: "DiningAreas");

            migrationBuilder.AlterColumn<string>(
                name: "tableNumber",
                table: "Tables",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_Tables_areaID",
                table: "Tables",
                column: "areaID");
        }
    }
}
