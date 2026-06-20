using System;

namespace WebBanDienThoai.Models
{
    public class Notification
    {
        public int Id { get; set; }
        public string UserId { get; set; } = null!; // Gửi cho ai (Id của ApplicationUser hoặc "All")
        public string Title { get; set; } = null!;  // Tiêu đề: "Sản phẩm giảm giá sốc!"
        public string Message { get; set; } = null!; // Nội dung chi tiết
        public string? RedirectUrl { get; set; }   // Đường dẫn bấm vào xem sản phẩm (/Product/Details/5)
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;   // Trạng thái đã đọc hay chưa
    }
}