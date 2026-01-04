using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class ProductRatingReport
    {
        public int Id { get; set; }
        public int ProductRatingId { get; set; }
        public ProductRating ProductRating { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required, StringLength(200)]
        public string Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
