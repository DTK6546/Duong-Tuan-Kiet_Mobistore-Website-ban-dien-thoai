using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class WarrantyOption
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;  // Ví dụ: "Bảo hành 24 tháng rơi vỡ"

        [Range(0, 100000000)]
        public decimal Price { get; set; }               // Số tiền cộng thêm

        [Required]
        public int ProductId { get; set; }               // Gắn với sản phẩm nào
        public Product? Product { get; set; }

        public bool IsActive { get; set; } = true;       // Có đang áp dụng hay không
    }
}
