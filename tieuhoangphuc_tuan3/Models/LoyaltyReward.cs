using System;
using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class LoyaltyReward
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } // Ví dụ: "Voucher giảm giá 50k"

        public string? Description { get; set; }

        [Required]
        public int PointsRequired { get; set; } // Số điểm cần để đổi

        [Required]
        public decimal DiscountAmount { get; set; } // Số tiền giảm thực tế khi áp mã

        public string? CouponCodePrefix { get; set; } = "LOYALTY"; // Tiền tố sinh mã tự động

        public bool IsActive { get; set; } = true;
    }
}