using System;

namespace WebBanDienThoai.Models
{
    public class Coupon
    {
        public int Id { get; set; }

        public string Code { get; set; }        // Mã giảm giá (VD: SALE50)
        public decimal? DiscountAmount { get; set; }   // Giảm theo tiền
        public int? DiscountPercent { get; set; }      // Giảm theo %
        public decimal MinOrderValue { get; set; }     // Đơn tối thiểu
        public int Quantity { get; set; }              // Số lượt được dùng
        public int CurrentUsage { get; set; }          // Đã dùng bao nhiêu lượt
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
    }
}
