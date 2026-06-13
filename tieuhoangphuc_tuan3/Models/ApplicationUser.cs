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
            >= 3000 => "Kim Cương", // Đã tích lũy từ 3.000 điểm
            >= 1500 => "Vàng",      // Đã tích lũy từ 1.500 điểm
            >= 500 => "Bạc",       // Đã tích lũy từ 500 điểm
            _ => "Đồng"       // Mới đăng ký hoặc dưới 500 điểm
        };

        // Tỷ lệ giảm giá đặc quyền áp dụng trực tiếp vào tổng đơn hàng
        public decimal TierDiscountRate => MemberTier switch
        {
            "Kim Cương" => 0.05m, // Giảm thêm 5% tổng hóa đơn
            "Vàng" => 0.03m, // Giảm thêm 3% tổng hóa đơn
            "Bạc" => 0.01m, // Giảm thêm 1% tổng hóa đơn
            _ => 0.00m  // Hạng Đồng: giữ nguyên giá
        };
    }
}