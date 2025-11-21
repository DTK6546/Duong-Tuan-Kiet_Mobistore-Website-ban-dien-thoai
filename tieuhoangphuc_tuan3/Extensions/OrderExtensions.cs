using WebBanDienThoai.Models;

namespace WebBanDienThoai.Extensions
{
    public static class OrderExtensions
    {
        public static string ToVietnameseStatus(this OrderStatus status)
        {
            switch (status)
            {
                case OrderStatus.ChoXacNhan: return "Chờ xác nhận";
                case OrderStatus.DangXuLy: return "Đang xử lý";
                case OrderStatus.DangGiao: return "Đang giao hàng";
                case OrderStatus.DaGiao: return "Đã giao hàng";
                case OrderStatus.DaHuy: return "Đã hủy";
                case OrderStatus.HoanTat: return "Hoàn tất";
                case OrderStatus.TraHang: return "Trả hàng";
                default: return "Không xác định";
            }
        }
    }
}
