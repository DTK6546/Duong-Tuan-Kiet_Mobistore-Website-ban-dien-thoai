using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;
using Rotativa.AspNetCore;

namespace WebBanDienThoai.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == user.Id)
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id && o.UserId == user.Id)
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Include thêm cả OrderDetails để lấy danh sách sản phẩm cần trả kho
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();

            // Chỉ cho huỷ khi đơn chưa đi giao
            if (order.Status == OrderStatus.ChoXacNhan || order.Status == OrderStatus.DangXuLy)
            {
                order.Status = OrderStatus.DaHuy;
                _context.Update(order);

                // 🧭 HOÀN LẠI SỐ LƯỢNG SẢN PHẨM VÀO KHO KHI HỦY ĐƠN
                if (order.OrderDetails != null)
                {
                    foreach (var detail in order.OrderDetails)
                    {
                        if (detail.VariantId.HasValue)
                        {
                            var variant = await _context.ProductVariants.FindAsync(detail.VariantId.Value);
                            if (variant != null)
                            {
                                variant.Stock += detail.Quantity; // Cộng trả lại kho biến thể
                                _context.ProductVariants.Update(variant);
                            }
                        }
                        else
                        {
                            var dbProduct = await _context.Products.FindAsync(detail.ProductId);
                            if (dbProduct != null)
                            {
                                dbProduct.Quantity += detail.Quantity; // Cộng trả lại kho sản phẩm chính
                                _context.Products.Update(dbProduct);
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceived(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);
            if (order == null) return NotFound();

            if (order.Status == OrderStatus.DaGiao)
            {
                order.Status = OrderStatus.HoanTat;
                _context.Update(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnOrder(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);
            if (order == null) return NotFound();

            // Chỉ cho trả hàng khi đã hoàn tất
            if (order.Status == OrderStatus.HoanTat)
            {
                order.Status = OrderStatus.TraHang;
                _context.Update(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Invoice(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .Where(o => o.Id == id && o.UserId == user.Id)
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            return View(order);
        }

        public async Task<IActionResult> ExportInvoicePdf(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .Where(o => o.Id == id && o.UserId == user.Id)
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            return new ViewAsPdf("Invoice", order)
            {
                FileName = $"HoaDon_{order.Id}.pdf"
            };
        }
    }
}
