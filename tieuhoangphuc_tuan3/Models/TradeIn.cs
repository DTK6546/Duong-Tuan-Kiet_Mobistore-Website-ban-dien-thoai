using System;
using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class TradeIn
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "Guest"; // Lưu ID khách hàng nếu đã đăng nhập

        [Required(ErrorMessage = "Vui lòng nhập tên dòng máy cũ")]
        public string OldDeviceName { get; set; } = null!; // Ví dụ: iPhone 13 Pro Max

        public string CosmeticCondition { get; set; } = null!; // Tình trạng ngoại quan (Loại A, B, C)
        public string Functionality { get; set; } = null!; // Tình trạng tính năng (Hoạt động tốt / Lỗi nhẹ)

        public decimal EstimatedValue { get; set; } // Số tiền máy cũ được định giá thu mua
        public int TargetProductId { get; set; } // ID sản phẩm muốn lên đời

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsApplied { get; set; } = false; // Trạng thái đã được trừ vào đơn hàng chưa
    }
}