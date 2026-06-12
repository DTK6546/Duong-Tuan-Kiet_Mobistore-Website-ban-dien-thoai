using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    /// <inheritdoc />
    public partial class FixDecimalAndStoresCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Districts_DistrictId",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Provinces_ProvinceId",
                table: "Stores");

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Districts_DistrictId",
                table: "Stores",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Provinces_ProvinceId",
                table: "Stores",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Districts_DistrictId",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Provinces_ProvinceId",
                table: "Stores");

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Districts_DistrictId",
                table: "Stores",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Provinces_ProvinceId",
                table: "Stores",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
