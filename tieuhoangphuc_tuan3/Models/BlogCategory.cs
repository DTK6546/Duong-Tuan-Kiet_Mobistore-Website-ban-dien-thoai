using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class BlogCategory
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên danh mục không được để trống")]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(100)]
        public string? Slug { get; set; } // Phục vụ URL đẹp: huong-dan-su-dung

        public ICollection<BlogPost>? BlogPosts { get; set; }
    }
}