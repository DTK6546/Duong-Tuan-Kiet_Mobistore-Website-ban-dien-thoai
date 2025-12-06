using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class WarrantyOptionController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WarrantyOptionController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? productId)
        {
            var query = _context.WarrantyOptions
                .Include(w => w.Product)
                .AsQueryable();

            if (productId.HasValue)
            {
                query = query.Where(w => w.ProductId == productId.Value);
            }

            ViewBag.Products = new SelectList(_context.Products, "Id", "Name", productId);

            var list = await query.OrderByDescending(w => w.Id).ToListAsync();
            return View(list);
        }

        [HttpGet]
        public IActionResult Add()
        {
            ViewBag.Products = new SelectList(_context.Products, "Id", "Name");
            return View(new WarrantyOption());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(WarrantyOption model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = new SelectList(_context.Products, "Id", "Name", model.ProductId);
                return View(model);
            }

            _context.WarrantyOptions.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã thêm gói bảo hành.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Update(int id)
        {
            var w = await _context.WarrantyOptions.FindAsync(id);
            if (w == null) return NotFound();

            ViewBag.Products = new SelectList(_context.Products, "Id", "Name", w.ProductId);
            return View(w);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(WarrantyOption model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = new SelectList(_context.Products, "Id", "Name", model.ProductId);
                return View(model);
            }

            _context.WarrantyOptions.Update(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật gói bảo hành.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var w = await _context.WarrantyOptions.FindAsync(id);
            if (w != null)
            {
                _context.WarrantyOptions.Remove(w);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Đã xóa gói bảo hành.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
