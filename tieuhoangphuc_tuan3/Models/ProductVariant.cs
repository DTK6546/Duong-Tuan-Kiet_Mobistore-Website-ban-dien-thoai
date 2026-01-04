using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class ProductVariant
    {
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey(nameof(ProductId))]
        public Product? Product { get; set; }

        [Required]
        [StringLength(100)]
        public string Color { get; set; }     // Màu sắc

        [Required]
        [StringLength(100)]
        public string Storage { get; set; }   // Dung lượng (256GB, 512GB, ...)

        [Required]
        public decimal Price { get; set; }    // Giá cho biến thể này

        public int Stock { get; set; }        // Tồn kho

        public string? Ram { get; set; }               // RAM theo biến thể
        public string? StorageAvailable { get; set; }  // Dung lượng còn lại (khả dụng)

        [StringLength(50)]
        public string? Sku { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        // 🔹 Map cho code phía trên: dùng Storage như Capacity
        [NotMapped]
        public string Capacity => Storage;

        // 🔹 Map cho code phía trên: tạm dùng Price như DiscountedPrice
        [NotMapped]
        public decimal DiscountedPrice => Price;
    }
}
