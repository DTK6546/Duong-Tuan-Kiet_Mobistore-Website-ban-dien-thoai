using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _db;

        public OrderController(ApplicationDbContext db)
        {
            _db = db;
        }

        public IActionResult Index(string search, int? status)
        {
            var orders = _db.Orders
                .Include(o => o.ApplicationUser)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                orders = orders.Where(o => o.ApplicationUser.FullName.Contains(search) || o.Id.ToString() == search);

            if (status != null)
                orders = orders.Where(o => (int)o.Status == status.Value);

            return View(orders.ToList());
        }

        public IActionResult Details(int id)
        {
            var order = _db.Orders
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.Shipper)
                .Include(o => o.OrderLogs)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
                return NotFound();

            ViewBag.Shippers = _db.Shippers.Where(s => s.IsActive).ToList();
            return View(order);
        }

        [HttpPost]
        public IActionResult UpdateStatus(int id, OrderStatus status)
        {
            var order = _db.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.OrderLogs)
                .FirstOrDefault(o => o.Id == id);

            if (order == null) return NotFound();

            order.Status = status;

            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();

            string description = status switch
            {
                OrderStatus.ChoXacNhan => "Đơn hàng đã được khởi tạo thành công và chờ tổng đài xác nhận.",
                OrderStatus.DangXuLy => "Đơn hàng đã được xác nhận. Nhân viên đang kiểm kho và đóng gói.",
                OrderStatus.DangGiao => "Bưu tá đã lấy hàng thành công và bưu kiện đang trên đường vận chuyển.",
                OrderStatus.DaGiao => "Gói hàng đã được giao đến bưu cục phát. Shipper chuẩn bị phát hàng.",
                OrderStatus.HoanTat => "Người mua xác nhận đã nhận hàng thành công. Đơn hàng hoàn tất.",
                OrderStatus.DaHuy => "Đơn hàng đã bị hủy bỏ trên hệ thống.",
                OrderStatus.TraHang => "Hệ thống tiếp nhận yêu cầu trả hàng / hoàn tiền từ người mua.",
                _ => "Trạng thái đơn hàng có sự thay đổi."
            };

            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                StatusDescription = description,
                LogDate = DateTime.Now,
                Location = "Hệ thống MobiStore"
            });

            if (status == OrderStatus.HoanTat || status == OrderStatus.TraHang)
            {
                foreach (var item in order.OrderDetails)
                {
                    int delta = status == OrderStatus.HoanTat ? -item.Quantity : item.Quantity;
                    ProductVariant variant = null;
                    if (item.VariantId.HasValue)
                    {
                        // 🛠️ ĐÃ FIX LỖI BIẾN BIÊN DỊCH TẠI ĐÂY:
                        variant = _db.ProductVariants.FirstOrDefault(v => v.Id == item.VariantId.Value);
                    }
                    var product = _db.Products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (variant != null) { variant.Stock += delta; }
                    if (product != null)
                    {
                        product.Quantity += delta;
                        if (status == OrderStatus.HoanTat) { product.LastExportDate = DateTime.Now; }
                    }
                }

                // =========================================================================
                // ✨ CHỨC NĂNG 2: TỰ ĐỘNG TÍNH TOÁN CỘNG ĐIỂM THƯỞNG KHI ĐƠN HOÀN TẤT
                // Quy đổi: Mỗi 100.000 đ giá trị đơn hàng thực tế mang về cho khách 1 điểm thưởng
                // =========================================================================
                if (status == OrderStatus.HoanTat)
                {
                    var user = _db.ApplicationUsers.FirstOrDefault(u => u.Id == order.UserId);
                    if (user != null)
                    {
                        int pointsEarned = (int)(order.TotalPrice / 100000);
                        user.CurrentPoints += pointsEarned;
                        user.RankingPoints += pointsEarned; // Tăng điểm trọn đời để thăng hạng VIP
                    }
                }
            }

            _db.SaveChanges();
            TempData["SuccessMessage"] = "Trạng thái đơn hàng và nhật ký hành trình đã được cập nhật!";
            return RedirectToAction("Details", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateLogistics(int orderId, int shipperId, string trackingNumber, string initialLogDescription)
        {
            var order = _db.Orders
                .Include(o => o.OrderLogs)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null) return NotFound();

            order.ShipperId = shipperId;
            order.TrackingNumber = (trackingNumber ?? "").Trim();

            if (order.Status == OrderStatus.ChoXacNhan)
            {
                order.Status = OrderStatus.DangXuLy;
            }

            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();

            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                StatusDescription = !string.IsNullOrWhiteSpace(initialLogDescription)
                    ? initialLogDescription.Trim()
                    : "Đơn hàng đã được bàn giao thành công cho nhân viên giao vận nội bộ.",
                LogDate = DateTime.Now,
                Location = "Kho tổng MobiStore"
            });

            _db.SaveChanges();

            TempData["SuccessMessage"] = "Đã phân bổ Shipper phụ trách và phát hành mã vận đơn thành công!";
            return RedirectToAction("Details", new { id = orderId });
        }

        public IActionResult CreateShipper()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShipper(Shipper shipper)
        {
            if (ModelState.IsValid)
            {
                shipper.IsActive = true;
                _db.Shippers.Add(shipper);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã thêm shipper {shipper.FullName} thành công!";
                return RedirectToAction("Index");
            }
            return View(shipper);
        }
    }
}