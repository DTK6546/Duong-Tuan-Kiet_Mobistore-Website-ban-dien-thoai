namespace WebBanDienThoai.Models
{
    public class ProductWithSoldCount : Product
    {
        public int SoldCount { get; set; }
        public ProductSpecs? Specs { get; set; }
        public string? ServiceCommitment { get; set; }
        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();

    }
}
