namespace WebBanDienThoai.Models
{
    public class ProductRatingVote
    {
        public int Id { get; set; }
        public int ProductRatingId { get; set; }
        public ProductRating ProductRating { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public bool IsLike { get; set; } // true=like, false=dislike
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
