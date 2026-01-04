using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class Province
    {
        public int Id { get; set; }

        [Required, StringLength(20)]
        public string Code { get; set; } = ""; // HCM, HN...

        [Required, StringLength(100)]
        public string Name { get; set; } = ""; // TP.HCM, Hà Nội...

        public ICollection<District>? Districts { get; set; }
    }
}
