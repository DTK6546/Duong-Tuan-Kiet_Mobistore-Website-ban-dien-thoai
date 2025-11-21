using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class SubCategory
    {
        public int Id { get; set; }

        [Required, StringLength(50)]
        public string Name { get; set; }

        // Khóa ngoại tới Category
        [Required]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        // Nếu muốn sau này liên kết với Product
        public List<Product>? Products { get; set; }
    }
}
