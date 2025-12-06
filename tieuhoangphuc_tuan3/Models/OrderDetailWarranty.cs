using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class OrderDetailWarranty
    {
        public int Id { get; set; }

        public int OrderDetailId { get; set; }
        public OrderDetail OrderDetail { get; set; }

        public int WarrantyOptionId { get; set; }

        public string Name { get; set; } = "";

        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        public int Months { get; set; }
    }
}
