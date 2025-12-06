using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

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
                .Where(o => o.UserId == user.Id)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .Where(o => o.Id == id && o.UserId == user.Id)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)   // ⭐ lấy luôn gói bảo hành
                .Include(o => o.Store)
                .FirstOrDefaultAsync();

            if (order == null)
                return NotFound();

            return View(order);
        }

        [HttpPost]
        public IActionResult CancelOrder(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.Status == OrderStatus.ChoXacNhan || order.Status == OrderStatus.DangXuLy)
            {
                order.Status = OrderStatus.DaHuy;
                _context.Update(order);
                _context.SaveChanges();
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public IActionResult ConfirmReceived(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.Status == OrderStatus.DaGiao)
            {
                order.Status = OrderStatus.HoanTat;
                _context.Update(order);
                _context.SaveChanges();
            }

            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public IActionResult ReturnOrder(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();

            if (order.Status == OrderStatus.HoanTat)
            {
                order.Status = OrderStatus.TraHang;
                _context.Update(order);
                _context.SaveChanges();
            }

            return RedirectToAction("Details", new { id });
        }
    }
}
