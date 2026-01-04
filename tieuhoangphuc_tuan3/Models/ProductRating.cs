using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // ✅ NEW: ảnh + vote + report
        public ICollection<ProductRatingImage> Images { get; set; } = new List<ProductRatingImage>();
        public ICollection<ProductRatingVote> Votes { get; set; } = new List<ProductRatingVote>();
        public ICollection<ProductRatingReport> Reports { get; set; } = new List<ProductRatingReport>();

        // ✅ NEW: cache (phục vụ sort “hữu ích”)
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }

        public ICollection<ProductRatingReply> Replies { get; set; } = new List<ProductRatingReply>();

        [NotMapped]
        public bool IsVerifiedPurchase { get; set; } // Nhãn "Đã mua"
    }
}
