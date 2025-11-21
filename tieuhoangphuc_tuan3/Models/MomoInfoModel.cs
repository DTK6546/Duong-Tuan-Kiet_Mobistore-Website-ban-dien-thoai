using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class MomoInfoModel
    {
        [Key]
        public int Id { get; set; }

        public int OrderId { get; set; } // Liên kết với Order.Id
        public string MomoOrderId { get; set; } // Lưu orderId từ MoMo

        public string OrderInfo { get; set; }
        public string FullName { get; set; }
        public decimal Amount { get; set; }
        public DateTime DatePaid { get; set; }

        [ForeignKey("OrderId")]
        public Order Order { get; set; }
    }
}