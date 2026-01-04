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

        // Danh sách đơn hàng
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

        // Chi tiết đơn hàng
        public IActionResult Details(int id)
        {
            var order = _db.Orders
        .Include(o => o.ApplicationUser) // Cần để hiện FullName khách hàng
        .Include(o => o.Store)
        .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Product)
        .Include(o => o.OrderDetails)
            .ThenInclude(od => od.Warranties) // Hiển thị các gói bảo hành khách đã mua             
        .FirstOrDefault(o => o.Id == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        // Cập nhật trạng thái đơn hàng
        [HttpPost]
        public IActionResult UpdateStatus(int id, OrderStatus status)
        {
            var order = _db.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefault(o => o.Id == id);

            if (order == null) return NotFound();

            // Cập nhật trạng thái đơn hàng
            order.Status = status;

            // 🧭 Nếu đơn hàng được hoàn tất => tự động trừ hàng tồn kho
            if (status == OrderStatus.HoanTat || status == OrderStatus.TraHang)
            {
                foreach (var item in order.OrderDetails)
                {
                    // xác định số lượng tăng/giảm
                    int delta = status == OrderStatus.HoanTat
                        ? -item.Quantity   // Hoàn tất: trừ kho
                        : item.Quantity;  // Trả hàng: cộng kho

                    ProductVariant variant = null;
                    if (item.VariantId.HasValue)
                    {
                        variant = _db.ProductVariants
                                     .FirstOrDefault(v => v.Id == item.VariantId.Value);
                    }

                    var product = _db.Products
                                     .FirstOrDefault(p => p.Id == item.ProductId);

                    // cập nhật biến thể (nếu có)
                    if (variant != null)
                    {
                        variant.Stock += delta;
                    }

                    // luôn cập nhật sản phẩm cha (nếu bạn muốn tồn kho tổng cũng thay đổi)
                    if (product != null)
                    {
                        product.Quantity += delta;

                        if (status == OrderStatus.HoanTat)
                        {
                            product.LastExportDate = DateTime.Now;
                        }
                    }
                }
            }

            _db.SaveChanges();
            TempData["SuccessMessage"] = "Trạng thái đơn hàng đã được cập nhật!";

            return RedirectToAction("Details", new { id = id });
        }

    }
}
