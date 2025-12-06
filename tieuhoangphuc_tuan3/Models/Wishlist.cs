using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class Wishlist
    {
        [Key]
        public int Id { get; set; }

        public int ProductId { get; set; }

        public string UserId { get; set; }
        public int? VariantId { get; set; }      
        public ProductVariant? Variant { get; set; } 

        [ForeignKey("ProductId")] 
        public Product Product { get; set; }
    }
}
