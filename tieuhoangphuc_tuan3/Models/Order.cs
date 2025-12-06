using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WebBanDienThoai.Models
{
    public enum DeliveryMethod
    {
        ShipToHome = 0,     // Giao hàng tận nơi
        PickupAtStore = 1   // Nhận tại cửa hàng
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

        [ForeignKey("UserId")]
        [ValidateNever]
        public ApplicationUser ApplicationUser { get; set; }
        public List<OrderDetail> OrderDetails { get; set; }
    }
}