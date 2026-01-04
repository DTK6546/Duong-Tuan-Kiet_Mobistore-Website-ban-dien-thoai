namespace WebBanDienThoai.Models
{
    public class ProductRatingImage
    {
        public int Id { get; set; }
        public int ProductRatingId { get; set; }
        public ProductRating ProductRating { get; set; }

        public string Url { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
