using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Employer.Controllers
{
    [Area("Employer")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        public OrderController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var orders = _context.Orders
                .Include(o => o.ApplicationUser)
                .OrderByDescending(o => o.Id)
                .ToList();
            return View(orders);
        }

        public IActionResult Details(int id)
        {
            var order = _context.Orders
                .Include(o => o.OrderDetails)
                .ThenInclude(od => od.Product)
                .Include(o => o.ApplicationUser)
                .FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateStatus(int id, int status)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);
            if (order == null) return NotFound();

            // CHỈ CHO NHÂN VIÊN cập nhật trạng thái: Đang giao, Đã giao
            if (status == (int)OrderStatus.DangGiao || status == (int)OrderStatus.DaGiao)
            {
                order.Status = (OrderStatus)status;
                _context.Update(order);
                _context.SaveChanges();
            }

            return RedirectToAction("Details", new { id });
        }
    }
}
