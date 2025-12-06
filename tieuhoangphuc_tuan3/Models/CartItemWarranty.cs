namespace WebBanDienThoai.Models
{
    public class CartItemWarranty
    {
        public int WarrantyOptionId { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public int Months { get; set; }
    }
}
