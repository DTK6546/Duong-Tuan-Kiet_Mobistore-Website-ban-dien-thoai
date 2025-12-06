using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebBanDienThoai.Migrations
{
    /// <inheritdoc />
    public partial class CauHinh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductSpecs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Os = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cpu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CpuSpeed = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Gpu = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ram = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Storage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StorageAvailable = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactLimit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RearCameraResolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrontCameraResolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RearVideoModes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrontVideoModes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RearCameraFeatures = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FrontCameraFeatures = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScreenTech = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScreenResolution = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScreenSize = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScreenBrightness = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ScreenGlass = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatteryUsageTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatteryType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxChargePower = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BatteryTech = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Security = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SpecialFeatures = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WaterDustResist = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Recorder = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VideoFormats = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AudioFormats = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MobileNetwork = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Sim = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Wifi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Gps = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Bluetooth = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChargingPort = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeadphoneJack = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OtherConnections = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DesignStyle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Material = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dimensions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Weight = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReleaseTime = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BrandInfo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSpecs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSpecs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSpecs_ProductId",
                table: "ProductSpecs",
                column: "ProductId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductSpecs");
        }
    }
}
