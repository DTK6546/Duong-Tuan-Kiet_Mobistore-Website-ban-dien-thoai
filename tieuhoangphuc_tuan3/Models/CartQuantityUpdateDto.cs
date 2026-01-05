namespace WebBanDienThoai.Models
{
    public class CartQuantityUpdateDto
    {
        public int ProductId { get; set; }
        public int? VariantId { get; set; }
        public int Quantity { get; set; }
    }
}
