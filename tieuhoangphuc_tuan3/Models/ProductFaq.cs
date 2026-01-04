using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class ProductFaq
    {
        public int Id { get; set; }

        // null = FAQ chung toàn shop (đổi trả, VAT, bảo hành...)
        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        [Required, StringLength(300)]
        public string Question { get; set; }

        [Required, StringLength(2000)]
        public string Answer { get; set; }

        public int SortOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

    }
}
