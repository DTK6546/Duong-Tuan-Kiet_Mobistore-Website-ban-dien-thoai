using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebBanDienThoai.Models
{
    public enum DeliveryMethod
    {
        ShipToHome = 0,     // Giao hàng tận nơi
        PickupAtStore = 1   // Nhận tại cửa hàng
    }

    // ✨ BỔ SUNG TRẠNG THÁI THANH TOÁN
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
        public DeliveryMethod DeliveryMethod { get; set; } = DeliveryMethod.ShipToHome;
        public int? StoreId { get; set; }
        public Store? Store { get; set; }
        public string ProvinceCode { get; set; }
        public string DistrictCode { get; set; }
        public decimal ShippingFee { get; set; }

        // =========================================================================
        // ✨ CẬP NHẬT CHỨC NĂNG 2 & 3: LƯU PHƯƠNG THỨC COD / TRẢ GÓP 0%
        // =========================================================================
        public string PaymentMethod { get; set; } = "COD"; // "COD" hoặc "Installment"
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.ChuaThanhToan;

        public string? InstallmentBank { get; set; }  // Ngân hàng trả góp (ví dụ: Techcombank)
        public int? InstallmentMonths { get; set; }   // Kỳ hạn trả góp (ví dụ: 6, 12 tháng)
        // =========================================================================

        [ForeignKey("UserId")]
        [ValidateNever]
        public ApplicationUser ApplicationUser { get; set; }
        public List<OrderDetail> OrderDetails { get; set; }
    }
}