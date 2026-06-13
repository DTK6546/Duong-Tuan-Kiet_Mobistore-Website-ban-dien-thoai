using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class FlashSale
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal SalePrice { get; set; } // Giá bán trong thời gian Flash Sale
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int SaleStock { get; set; }     // Số lượng giới hạn mở bán Flash Sale
        public int SoldCount { get; set; }     // Số lượng đã bán được

        [ForeignKey("ProductId")]
        public Product? Product { get; set; }

        // Kiểm tra xem chương trình còn hiệu lực không
        public bool IsActive => DateTime.Now >= StartTime && DateTime.Now <= EndTime && SoldCount < SaleStock;
    }
}