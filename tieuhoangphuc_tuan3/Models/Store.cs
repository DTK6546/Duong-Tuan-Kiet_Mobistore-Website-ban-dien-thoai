using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class Store
    {
        public int Id { get; set; }

        [Required, StringLength(200)]
        [Display(Name = "Tên cửa hàng")]
        public string Name { get; set; } = "";

        [Required, StringLength(255)]
        [Display(Name = "Địa chỉ (số nhà, đường)")]
        public string Address { get; set; } = "";

        [StringLength(100)]
        [Display(Name = "Phường/Xã")]
        public string? Ward { get; set; }

        // ✅ Dùng FK thay vì string
        [Required]
        [Display(Name = "Tỉnh/TP")]
        public int ProvinceId { get; set; }

        [ForeignKey(nameof(ProvinceId))]
        public Province? Province { get; set; }

        [Required]
        [Display(Name = "Quận/Huyện")]
        public int DistrictId { get; set; }

        [ForeignKey(nameof(DistrictId))]
        public District? District { get; set; }

        [StringLength(20)]
        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Giờ mở cửa")]
        public string? OpenHours { get; set; }

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;
    }
}
