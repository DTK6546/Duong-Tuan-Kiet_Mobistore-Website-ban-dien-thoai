using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class ProductSpecs
    {
        public int Id { get; set; }

        // FK tới Product
        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }
        [ValidateNever]
        public Product? Product { get; set; }

        // ===========================
        // 1) CẤU HÌNH & BỘ NHỚ
        // ===========================
        public string? Os { get; set; }
        public string? Cpu { get; set; }
        public string? CpuSpeed { get; set; }
        public string? Gpu { get; set; }
        public string? Ram { get; set; }
        public string? Storage { get; set; }
        public string? StorageAvailable { get; set; }
        public string? ContactLimit { get; set; }

        // ===========================
        // 2) CAMERA & MÀN HÌNH
        // ===========================
        public string? RearCameraResolution { get; set; }
        public string? FrontCameraResolution { get; set; }
        public string? RearVideoModes { get; set; }
        public string? FrontVideoModes { get; set; }
        public string? RearCameraFeatures { get; set; }
        public string? FrontCameraFeatures { get; set; }
        public string? ScreenTech { get; set; }
        public string? ScreenResolution { get; set; }
        public string? ScreenSize { get; set; }
        public string? ScreenBrightness { get; set; }
        public string? ScreenGlass { get; set; }

        // ===========================
        // 3) PIN & SẠC
        // ===========================
        public string? BatteryUsageTime { get; set; }
        public string? BatteryType { get; set; }
        public string? MaxChargePower { get; set; }
        public string? BatteryTech { get; set; }

        // ===========================
        // 4) TIỆN ÍCH
        // ===========================
        public string? Security { get; set; }
        public string? SpecialFeatures { get; set; }
        public string? WaterDustResist { get; set; }
        public string? Recorder { get; set; }
        public string? VideoFormats { get; set; }
        public string? AudioFormats { get; set; }

        // ===========================
        // 5) KẾT NỐI
        // ===========================
        public string? MobileNetwork { get; set; }
        public string? Sim { get; set; }
        public string? Wifi { get; set; }
        public string? Gps { get; set; }
        public string? Bluetooth { get; set; }
        public string? ChargingPort { get; set; }
        public string? HeadphoneJack { get; set; }
        public string? OtherConnections { get; set; }

        // ===========================
        // 6) THIẾT KẾ & CHẤT LIỆU
        // ===========================
        public string? DesignStyle { get; set; }
        public string? Material { get; set; }
        public string? Dimensions { get; set; }
        public string? Weight { get; set; }
        public string? ReleaseTime { get; set; }
        public string? BrandInfo { get; set; }
    }
}
