using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class ProductPriceHistory
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } // Mốc giá tại thời điểm đó

        public DateTime ChangeDate { get; set; } // Ngày thay đổi
    }
}