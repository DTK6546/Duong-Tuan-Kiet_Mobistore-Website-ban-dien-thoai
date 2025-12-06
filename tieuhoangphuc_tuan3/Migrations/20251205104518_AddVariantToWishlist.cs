using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantToWishlist : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VariantId",
                table: "Wishlists",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Wishlists_VariantId",
                table: "Wishlists",
                column: "VariantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Wishlists_ProductVariants_VariantId",
                table: "Wishlists",
                column: "VariantId",
                principalTable: "ProductVariants",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Wishlists_ProductVariants_VariantId",
                table: "Wishlists");

            migrationBuilder.DropIndex(
                name: "IX_Wishlists_VariantId",
                table: "Wishlists");

            migrationBuilder.DropColumn(
                name: "VariantId",
                table: "Wishlists");
        }
    }
}
