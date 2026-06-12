namespace WebBanDienThoai.Models
{
    public class CartItem
    {
        public string ImageUrl { get; set; }
        public int ProductId { get; set; }
        public int? VariantId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public int AvailableStock { get; set; }
        public List<CartItemWarranty> Warranties { get; set; } = new();
        // ✨ CHỨC NĂNG 1: ĐÁNH DẤU SẢN PHẨM LƯU LẠI MUA SAU
        public bool IsSaveForLater { get; set; } = false;
    }
}