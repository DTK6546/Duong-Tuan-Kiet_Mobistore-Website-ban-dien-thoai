using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    /// <inheritdoc />
    public partial class tintuc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHot",
                table: "News",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPromotion",
                table: "News",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHot",
                table: "News");

            migrationBuilder.DropColumn(
                name: "IsPromotion",
                table: "News");
        }
    }
}
