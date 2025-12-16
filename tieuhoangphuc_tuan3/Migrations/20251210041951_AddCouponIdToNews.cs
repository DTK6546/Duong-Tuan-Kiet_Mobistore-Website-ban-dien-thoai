using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponIdToNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CouponId",
                table: "News",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_News_CouponId",
                table: "News",
                column: "CouponId");

            migrationBuilder.AddForeignKey(
                name: "FK_News_Coupons_CouponId",
                table: "News",
                column: "CouponId",
                principalTable: "Coupons",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_News_Coupons_CouponId",
                table: "News");

            migrationBuilder.DropIndex(
                name: "IX_News_CouponId",
                table: "News");

            migrationBuilder.DropColumn(
                name: "CouponId",
                table: "News");
        }
    }
}
