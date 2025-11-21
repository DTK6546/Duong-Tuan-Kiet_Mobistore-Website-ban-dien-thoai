using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class ProductRatingReply
    {
        public int Id { get; set; }
        public int ProductRatingId { get; set; }
        public ProductRating ProductRating { get; set; }

        [Required]
        public string Content { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

    }
}
