using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    /// <inheritdoc />
    public partial class AddViewsToNews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Views",
                table: "News",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Views",
                table: "News");
        }
    }
}
