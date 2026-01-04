using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebBanDienThoai.Models
{
    public class District
    {
        public int Id { get; set; }

        [Required]
        public int ProvinceId { get; set; }

        [ForeignKey(nameof(ProvinceId))]
        public Province? Province { get; set; }

        [Required, StringLength(30)]
        public string Code { get; set; } = "";  // Q1, Q7...

        [Required, StringLength(100)]
        public string Name { get; set; } = "";  // Quận 1, Quận 7...
    }
}
