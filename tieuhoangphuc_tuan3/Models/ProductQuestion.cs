using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class ProductQuestion
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        [Required, StringLength(800)]
        public string Question { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // duyệt hiển thị
        public bool IsApproved { get; set; } = true;

        public ICollection<ProductQuestionReply> Replies { get; set; } = new List<ProductQuestionReply>();
    }
}
