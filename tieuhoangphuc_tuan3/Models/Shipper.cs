namespace WebBanDienThoai.Models
{
    public class Shipper
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string VehicleNumber { get; set; } // Biển số xe
        public string CompanyName { get; set; }   // "Nội bộ MobiStore", "Giao Hàng Nhanh", "GHTK"
        public bool IsActive { get; set; } = true;

        public List<Order>? Orders { get; set; }
    }
}