using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class PriceAlert
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TargetPrice { get; set; } // Giá mong muốn (Bằng hoặc thấp hơn mức này thì báo)

        public string Email { get; set; } = string.Empty; // Email nhận thông báo
        public bool IsTriggered { get; set; } = false; // Đã bắn thông báo chưa
        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}