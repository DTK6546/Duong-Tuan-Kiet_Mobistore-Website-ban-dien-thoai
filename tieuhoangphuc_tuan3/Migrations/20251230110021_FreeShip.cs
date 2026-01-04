using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    /// <inheritdoc />
    public partial class FreeShip : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ExpressFee",
                table: "ShippingRates",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ExpressMaxDays",
                table: "ShippingRates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ExpressMinDays",
                table: "ShippingRates",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FreeShipMinOrder",
                table: "ShippingRates",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpressFee",
                table: "ShippingRates");

            migrationBuilder.DropColumn(
                name: "ExpressMaxDays",
                table: "ShippingRates");

            migrationBuilder.DropColumn(
                name: "ExpressMinDays",
                table: "ShippingRates");

            migrationBuilder.DropColumn(
                name: "FreeShipMinOrder",
                table: "ShippingRates");
        }
    }
}
