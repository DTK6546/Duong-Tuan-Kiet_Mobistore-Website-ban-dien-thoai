using System;
using System.ComponentModel.DataAnnotations;
namespace WebBanDienThoai.Models
{
    public class News
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        public string Title { get; set; }

        [StringLength(400)]
        public string Summary { get; set; }

        [Required]
        public string Content { get; set; }

        public string ImageUrl { get; set; }
        public int Views { get; set; } = 0;
        public bool IsHot { get; set; } = false;

        public bool IsPromotion { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public int? CouponId { get; set; }
        public Coupon? Coupon { get; set; }
    }
}
