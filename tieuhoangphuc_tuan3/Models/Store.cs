using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class Store
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Tên cửa hàng")]
        public string Name { get; set; }           // Ví dụ: TGDD 289 Hai Bà Trưng

        [Required]
        [StringLength(255)]
        [Display(Name = "Địa chỉ (số nhà, đường)")]
        public string Address { get; set; }        // Số nhà, tên đường

        [StringLength(100)]
        [Display(Name = "Phường/Xã")]
        public string Ward { get; set; }

        [StringLength(100)]
        [Display(Name = "Quận/Huyện")]
        public string District { get; set; }

        [StringLength(100)]
        [Display(Name = "Tỉnh/Thành")]
        public string Province { get; set; }

        [StringLength(20)]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }

        [StringLength(100)]
        [Display(Name = "Giờ mở cửa")]
        public string OpenHours { get; set; }      // Ví dụ: "8:00 - 21:30"

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;

    }
}
