namespace WebBanDienThoai.Models
{
    /// <summary>
    /// Trạng thái đơn hàng
    /// </summary>
    public enum OrderStatus
    {
        ChoXacNhan = 0,   // Chờ xác nhận
        DangXuLy = 1,     // Đang xử lý
        DangGiao = 2,     // Đang giao hàng
        DaGiao = 3,       // Đã giao hàng (khách chưa xác nhận)
        HoanTat = 4,      // Hoàn tất (khách đã nhận và xác nhận, cho phép đánh giá)
        DaHuy = 5,        // Đã hủy
        TraHang = 6       // Đã trả hàng (khách trả lại do lỗi hoặc không muốn dùng)
    }
}
