using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        public string FullName { get; set; }
        public string? Address { get; set; }
        public string? Age { get; set; }

        // =========================================================================
        // ✨ CHỨC NĂNG 2 & 3: QUẢN LÝ ĐIỂM TÍCH LŨY (LOYALTY) & HẠNG THÀNH VIÊN VIP
        // =========================================================================
        public int CurrentPoints { get; set; }   // Điểm khả dụng dùng để chi tiêu trừ tiền
        public int RankingPoints { get; set; }   // Điểm tích lũy trọn đời (dùng để xét hạng, không bị trừ)

        // Phân hạng thành viên dựa trên điểm trọn đời
        public string MemberTier => RankingPoints switch
        {
            >= 3000 => "Kim Cương",
            >= 1500 => "Vàng",
            >= 500 => "Bạc",
            _ => "Đồng"
        };

        // Tỷ lệ giảm giá đặc quyền áp dụng trực tiếp vào tổng đơn hàng
        public decimal TierDiscountRate => MemberTier switch
        {
            "Kim Cương" => 0.05m,
            "Vàng" => 0.03m,
            "Bạc" => 0.01m,
            _ => 0.00m
        };

        // =========================================================================
        // ✨ CHỨC NĂNG 8: THUỘC TÍNH KHÓA/CHẶN NGƯỜI DÙNG SPAMMER (MỚI)
        // =========================================================================
        public bool IsBanned { get; set; } = false;
    }
}