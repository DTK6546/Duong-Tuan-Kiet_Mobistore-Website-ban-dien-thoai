using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class BlogPost
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tiêu đề không được để trống")]
        [StringLength(250)]
        public string Title { get; set; }

        [Required(ErrorMessage = "Nội dung bài viết không được để trống")]
        public string Content { get; set; }

        [StringLength(500)]
        public string? Summary { get; set; } // Tóm tắt ngắn ngoài trang danh sách

        [StringLength(250)]
        public string? ThumbnailUrl { get; set; } // Ảnh đại diện bài viết

        [StringLength(250)]
        public string? VideoEmbedUrl { get; set; } // Chứa link nhúng Youtube: https://www.youtube.com/embed/xyz

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public int ViewCount { get; set; } = 0;

        // Khóa ngoại nối sang danh mục Blog
        [Required]
        public int BlogCategoryId { get; set; }
        [ForeignKey("BlogCategoryId")]
        public BlogCategory? BlogCategory { get; set; }

        public ICollection<BlogComment>? BlogComments { get; set; }
    }
}