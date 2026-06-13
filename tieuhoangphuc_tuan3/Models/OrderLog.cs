namespace WebBanDienThoai.Models
{
    public class OrderLog
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string StatusDescription { get; set; } // Ví dụ: "Đơn hàng đã đến kho phân loại Thủ Đức"
        public DateTime LogDate { get; set; } = DateTime.Now;
        public string? Location { get; set; }         // Vị trí gói hàng (Hồ Chí Minh, Hà Nội...)

        public Order Order { get; set; }
    }
}