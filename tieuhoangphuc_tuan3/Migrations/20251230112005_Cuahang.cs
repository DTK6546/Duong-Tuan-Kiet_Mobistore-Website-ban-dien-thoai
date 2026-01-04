using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    public partial class Cuahang : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Cho phép nullable các field string như bạn đang làm
            migrationBuilder.AlterColumn<string>(
                name: "Ward",
                table: "Stores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Stores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "OpenHours",
                table: "Stores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            // 2) Add ProvinceId/DistrictId NULLABLE (tránh default 0)
            migrationBuilder.AddColumn<int>(
                name: "ProvinceId",
                table: "Stores",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DistrictId",
                table: "Stores",
                type: "int",
                nullable: true);

            // 3) MAP dữ liệu từ string -> FK (ưu tiên match theo Name, có thêm fallback LIKE)
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM Provinces)
    THROW 50000, N'Bảng Provinces đang rỗng. Hãy seed Provinces trước khi migrate.', 1;

IF NOT EXISTS (SELECT 1 FROM Districts)
    THROW 50000, N'Bảng Districts đang rỗng. Hãy seed Districts trước khi migrate.', 1;

-- Map ProvinceId theo Name (accent-insensitive)
UPDATE s
SET ProvinceId = p.Id
FROM Stores s
JOIN Provinces p
  ON REPLACE(LTRIM(RTRIM(s.Province)), N'.', N'') COLLATE Latin1_General_100_CI_AI
   = REPLACE(LTRIM(RTRIM(p.Name)),     N'.', N'') COLLATE Latin1_General_100_CI_AI
WHERE s.ProvinceId IS NULL;

-- Fallback: map ProvinceId theo LIKE (ví dụ: 'TP. Hồ Chí Minh' match 'Hồ Chí Minh')
UPDATE s
SET ProvinceId = x.Id
FROM Stores s
CROSS APPLY (
    SELECT TOP 1 p.Id
    FROM Provinces p
    WHERE s.Province COLLATE Latin1_General_100_CI_AI LIKE N'%' + p.Name + N'%'
       OR p.Name    COLLATE Latin1_General_100_CI_AI LIKE N'%' + s.Province + N'%'
    ORDER BY LEN(p.Name) DESC
) x
WHERE s.ProvinceId IS NULL AND s.Province IS NOT NULL;

-- Nếu vẫn chưa map được -> gán Province mặc định (TOP 1)
DECLARE @defaultProvinceId INT = (SELECT TOP 1 Id FROM Provinces ORDER BY Id);
UPDATE Stores SET ProvinceId = @defaultProvinceId WHERE ProvinceId IS NULL;
");

            migrationBuilder.Sql(@"
-- Map DistrictId theo Name + đúng ProvinceId
UPDATE s
SET DistrictId = d.Id
FROM Stores s
JOIN Districts d
  ON d.ProvinceId = s.ProvinceId
 AND REPLACE(LTRIM(RTRIM(s.District)), N'.', N'') COLLATE Latin1_General_100_CI_AI
  = REPLACE(LTRIM(RTRIM(d.Name)),     N'.', N'') COLLATE Latin1_General_100_CI_AI
WHERE s.DistrictId IS NULL;

-- Fallback: map theo LIKE (tránh lệch 'Quận Gò Vấp' vs 'Gò Vấp')
UPDATE s
SET DistrictId = x.Id
FROM Stores s
CROSS APPLY (
    SELECT TOP 1 d.Id
    FROM Districts d
    WHERE d.ProvinceId = s.ProvinceId
      AND s.District COLLATE Latin1_General_100_CI_AI LIKE N'%' + d.Name + N'%'
    ORDER BY LEN(d.Name) DESC
) x
WHERE s.DistrictId IS NULL AND s.District IS NOT NULL;

-- Nếu vẫn NULL -> lấy 1 district mặc định thuộc province đó
UPDATE s
SET DistrictId = x.Id
FROM Stores s
CROSS APPLY (
    SELECT TOP 1 d.Id
    FROM Districts d
    WHERE d.ProvinceId = s.ProvinceId
    ORDER BY d.Id
) x
WHERE s.DistrictId IS NULL;
");

            // 4) Đổi sang NOT NULL sau khi đã map xong
            migrationBuilder.AlterColumn<int>(
                name: "ProvinceId",
                table: "Stores",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DistrictId",
                table: "Stores",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 5) Index + FK (khuyên dùng NO ACTION/RESTRICT để tránh xóa tỉnh/quận kéo theo xóa store)
            migrationBuilder.CreateIndex(
                name: "IX_Stores_ProvinceId",
                table: "Stores",
                column: "ProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_DistrictId",
                table: "Stores",
                column: "DistrictId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Provinces_ProvinceId",
                table: "Stores",
                column: "ProvinceId",
                principalTable: "Provinces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stores_Districts_DistrictId",
                table: "Stores",
                column: "DistrictId",
                principalTable: "Districts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 6) Cuối cùng mới drop cột string cũ
            migrationBuilder.DropColumn(
                name: "Province",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Stores");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add lại cột string
            migrationBuilder.AddColumn<string>(
                name: "Province",
                table: "Stores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Stores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // (tuỳ chọn) copy ngược lại từ FK -> string
            migrationBuilder.Sql(@"
UPDATE s
SET Province = ISNULL(p.Name, N''),
    District = ISNULL(d.Name, N'')
FROM Stores s
LEFT JOIN Provinces p ON p.Id = s.ProvinceId
LEFT JOIN Districts d ON d.Id = s.DistrictId;
");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Provinces_ProvinceId",
                table: "Stores");

            migrationBuilder.DropForeignKey(
                name: "FK_Stores_Districts_DistrictId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Stores_ProvinceId",
                table: "Stores");

            migrationBuilder.DropIndex(
                name: "IX_Stores_DistrictId",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "ProvinceId",
                table: "Stores");

            migrationBuilder.DropColumn(
                name: "DistrictId",
                table: "Stores");

            // trả lại string NOT NULL nếu bạn muốn đúng như cũ
            migrationBuilder.AlterColumn<string>(
                name: "Ward",
                table: "Stores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhoneNumber",
                table: "Stores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OpenHours",
                table: "Stores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
