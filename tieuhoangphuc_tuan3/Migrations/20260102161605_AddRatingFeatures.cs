using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    /// <inheritdoc />
    public partial class AddRatingFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductRatings_ProductId",
                table: "ProductRatings");

            migrationBuilder.AddColumn<int>(
                name: "DislikeCount",
                table: "ProductRatings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LikeCount",
                table: "ProductRatings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ProductRatingImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductRatingId = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRatingImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRatingImages_ProductRatings_ProductRatingId",
                        column: x => x.ProductRatingId,
                        principalTable: "ProductRatings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductRatingReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductRatingId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRatingReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRatingReports_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductRatingReports_ProductRatings_ProductRatingId",
                        column: x => x.ProductRatingId,
                        principalTable: "ProductRatings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductRatingVotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductRatingId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsLike = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductRatingVotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductRatingVotes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductRatingVotes_ProductRatings_ProductRatingId",
                        column: x => x.ProductRatingId,
                        principalTable: "ProductRatings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatings_ProductId_UserId",
                table: "ProductRatings",
                columns: new[] { "ProductId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatingImages_ProductRatingId",
                table: "ProductRatingImages",
                column: "ProductRatingId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatingReports_ProductRatingId_UserId",
                table: "ProductRatingReports",
                columns: new[] { "ProductRatingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatingReports_UserId",
                table: "ProductRatingReports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatingVotes_ProductRatingId_UserId",
                table: "ProductRatingVotes",
                columns: new[] { "ProductRatingId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatingVotes_UserId",
                table: "ProductRatingVotes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductRatingImages");

            migrationBuilder.DropTable(
                name: "ProductRatingReports");

            migrationBuilder.DropTable(
                name: "ProductRatingVotes");

            migrationBuilder.DropIndex(
                name: "IX_ProductRatings_ProductId_UserId",
                table: "ProductRatings");

            migrationBuilder.DropColumn(
                name: "DislikeCount",
                table: "ProductRatings");

            migrationBuilder.DropColumn(
                name: "LikeCount",
                table: "ProductRatings");

            migrationBuilder.CreateIndex(
                name: "IX_ProductRatings_ProductId",
                table: "ProductRatings",
                column: "ProductId");
        }
    }
}
