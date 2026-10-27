using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "birthdayOnly",
                table: "Promotions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "endTime",
                table: "Promotions",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "limitResetsYearly",
                table: "Promotions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "maxTotalUses",
                table: "Promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "maxUsesPerCustomer",
                table: "Promotions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "minSpend",
                table: "Promotions",
                type: "decimal(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "orderType",
                table: "Promotions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<TimeSpan>(
                name: "startTime",
                table: "Promotions",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "custBirthday",
                table: "Customer",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "birthdayOnly",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "endTime",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "limitResetsYearly",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "maxTotalUses",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "maxUsesPerCustomer",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "minSpend",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "orderType",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "startTime",
                table: "Promotions");

            migrationBuilder.DropColumn(
                name: "custBirthday",
                table: "Customer");
        }
    }
}
