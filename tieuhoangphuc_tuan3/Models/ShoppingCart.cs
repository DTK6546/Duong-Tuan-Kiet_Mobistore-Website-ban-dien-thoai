namespace WebBanDienThoai.Models
{
    public class ShoppingCart
    {
        public List<CartItem> Items { get; set; } = new List<CartItem>();
        public string? CouponCode { get; set; }      // Lưu mã giảm giá
        public decimal DiscountAmount { get; set; }  // Số tiền được giảm
        public void AddItem(CartItem item)
        {
            var existingItem = Items.FirstOrDefault(i =>
                i.ProductId == item.ProductId &&
                i.VariantId == item.VariantId);
            if (existingItem != null)
            {
                existingItem.Quantity += item.Quantity;
            }
            else
            {
                Items.Add(item);
            }
        }
        public void RemoveItem(int productId, int? variantId)
        {
            var item = Items.FirstOrDefault(i =>
                i.ProductId == productId &&
                i.VariantId == variantId);

            if (item != null)
            {
                Items.Remove(item);
            }
        }

        public void RemoveItem(int productId)
        {
            Items.RemoveAll(i => i.ProductId == productId);
        }
       
    }
}