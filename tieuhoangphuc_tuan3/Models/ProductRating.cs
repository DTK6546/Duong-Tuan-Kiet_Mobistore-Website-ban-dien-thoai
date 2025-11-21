using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class ProductRating
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Range(1, 5)]
        public int Stars { get; set; }

        [StringLength(1000)]
        public string Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public ICollection<ProductRatingReply> Replies { get; set; } = new List<ProductRatingReply>();

    }
}
