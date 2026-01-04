using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class ShippingRate
    {
        public int Id { get; set; }

        [Required] 
        public string ProvinceCode { get; set; } = ""; // HCM, HN...
        public string? DistrictCode { get; set; }                 // Q1... hoặc null (áp dụng toàn tỉnh)

        public decimal Fee { get; set; }                          // phí ship
        public int MinDays { get; set; }                          // ngày tối thiểu
        public int MaxDays { get; set; }                          // ngày tối đa
        public decimal ExpressFee { get; set; }          // phí ship nhanh
        public int ExpressMinDays { get; set; }          // ngày tối thiểu (nhanh)
        public int ExpressMaxDays { get; set; }          // ngày tối đa (nhanh)
        public decimal? FreeShipMinOrder { get; set; }   // >= số này thì free ship (nullable)

    }
}
