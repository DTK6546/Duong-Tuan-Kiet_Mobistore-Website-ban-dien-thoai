using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class InventoryLog
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        [Required]
        public string Type { get; set; } // "NHAP" (Nhập kho) hoặc "XUAT" (Xuất kho khi bán)

        [Required]
        public int Quantity { get; set; } // Số lượng biến động (Ví dụ: +10 hoặc -2)

        public string? Note { get; set; } // Lý do: "Khách mua đơn hàng #12", "Nhập hàng đầu tháng"

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? ActionBy { get; set; } // Người thực hiện (Admin, Hệ thống, hoặc tên nhân viên)
    }
}