using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    /// <inheritdoc />
    public partial class thang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetailWarranty_OrderDetails_OrderDetailId",
                table: "OrderDetailWarranty");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrderDetailWarranty",
                table: "OrderDetailWarranty");

            migrationBuilder.RenameTable(
                name: "OrderDetailWarranty",
                newName: "OrderDetailWarranties");

            migrationBuilder.RenameIndex(
                name: "IX_OrderDetailWarranty_OrderDetailId",
                table: "OrderDetailWarranties",
                newName: "IX_OrderDetailWarranties_OrderDetailId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrderDetailWarranties",
                table: "OrderDetailWarranties",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetailWarranties_OrderDetails_OrderDetailId",
                table: "OrderDetailWarranties",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrderDetailWarranties_OrderDetails_OrderDetailId",
                table: "OrderDetailWarranties");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrderDetailWarranties",
                table: "OrderDetailWarranties");

            migrationBuilder.RenameTable(
                name: "OrderDetailWarranties",
                newName: "OrderDetailWarranty");

            migrationBuilder.RenameIndex(
                name: "IX_OrderDetailWarranties_OrderDetailId",
                table: "OrderDetailWarranty",
                newName: "IX_OrderDetailWarranty_OrderDetailId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrderDetailWarranty",
                table: "OrderDetailWarranty",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderDetailWarranty_OrderDetails_OrderDetailId",
                table: "OrderDetailWarranty",
                column: "OrderDetailId",
                principalTable: "OrderDetails",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
