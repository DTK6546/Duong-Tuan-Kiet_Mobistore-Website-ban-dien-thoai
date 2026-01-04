using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class ProductQuestionReply
    {
        public int Id { get; set; }

        public int ProductQuestionId { get; set; }
        public ProductQuestion ProductQuestion { get; set; }

        [Required, StringLength(1000)]
        public string Content { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }
}
