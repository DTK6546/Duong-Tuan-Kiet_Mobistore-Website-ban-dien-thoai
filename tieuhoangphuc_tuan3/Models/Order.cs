using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebBanDienThoai.Models
{
    public class DeliveryMethod
    {
        public const int ShipToHome = 0;     // Giao hàng tận nơi
        public const int PickupAtStore = 1;   // Nhận tại cửa hàng
    }

    public enum PaymentStatus
    {
        ChuaThanhToan = 0,
        DaThanhToan = 1,
        ChoDoiSoat = 2
    }

    public class Order
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal Subtotal { get; set; }        // Tạm tính (SP + bảo hành, chưa giảm, chưa VAT)
        public decimal DiscountAmount { get; set; }  // Số tiền giảm
        public decimal VatAmount { get; set; }       // VAT
        public string? CouponCode { get; set; }      // Mã giảm giá dùng cho đơn
        public string ShippingAddress { get; set; }
        public string Notes { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.ChoXacNhan;
        public int DeliveryMethod { get; set; } = 0;
        public int? StoreId { get; set; }
        public Store? Store { get; set; }
        public string ProvinceCode { get; set; }
        public string DistrictCode { get; set; }
        public decimal ShippingFee { get; set; }

        public string PaymentMethod { get; set; } = "COD"; // "COD" hoặc "Installment"
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.ChuaThanhToan;

        public string? InstallmentBank { get; set; }  // Ngân hàng trả góp
        public int? InstallmentMonths { get; set; }   // Kỳ hạn trả góp

        // =========================================================================
        // ✨ CẬP NHẬT CHỨC NĂNG 1 & 3: QUAN LÝ SHIPPER & TRACKING MÃ VẬN ĐƠN THỰC
        // =========================================================================
        public int? ShipperId { get; set; }
        public Shipper? Shipper { get; set; } // Khóa ngoại liên kết sang bảng Shipper

        public string? TrackingNumber { get; set; } // Mã vận đơn điện tử thực tế từ API vận chuyển
        public List<OrderLog>? OrderLogs { get; set; } // Nhật ký hành trình real-time

        // =========================================================================
        // 💸 CẬP NHẬT CHỨC NĂNG 6: LƯU VẾT GIẢM TRỪ THU CŨ ĐỔI MỚI (TRADE-IN)
        // =========================================================================
        public int? TradeInId { get; set; } // Khóa ngoại kết nối yêu cầu khảo sát máy cũ
        public decimal TradeInDiscount { get; set; } = 0m; // Số tiền được trợ giá khấu trừ máy cũ

        [ForeignKey("UserId")]
        [ValidateNever]
        public ApplicationUser ApplicationUser { get; set; }
        public List<OrderDetail> OrderDetails { get; set; }
    }
}