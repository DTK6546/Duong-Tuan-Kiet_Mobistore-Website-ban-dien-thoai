using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductSpecsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductSpecsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===========================
        // INDEX: Liệt kê sản phẩm + trạng thái cấu hình
        // ===========================
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        public async Task<IActionResult> Index(string searchTerm)
        {
            var query = _context.Products
                .Include(p => p.Specs)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p =>
                    p.Name.Contains(searchTerm) ||
                    (p.Description != null && p.Description.Contains(searchTerm)));
            }

            var products = await query.ToListAsync();

            ViewBag.CurrentSearch = searchTerm;
            return View(products);
        }

        // ===========================
        // GET: Chỉnh sửa cấu hình 1 sản phẩm
        // /Admin/ProductSpecs/Edit/5  (5 = productId)
        // ===========================
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)  // id = ProductId
        {
            var product = await _context.Products
                .Include(p => p.Specs)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            ViewBag.ProductId = product.Id;
            ViewBag.ProductName = product.Name;

            // Nếu chưa có specs thì tạo object trống để bind lên form
            var specs = product.Specs ?? new ProductSpecs
            {
                ProductId = product.Id
            };

            return View(specs);
        }

        // ===========================
        // POST: Lưu cấu hình 1 sản phẩm
        // ===========================
        [Authorize(Roles = SD.Role_Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductSpecs model)
        {
            if (id != model.ProductId)
                return BadRequest();

            var product = await _context.Products
                .Include(p => p.Specs)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            // 👇 BỎ VALIDATE CHO NAVIGATION PROPERTY
            ModelState.Remove(nameof(ProductSpecs.Product));

            if (!ModelState.IsValid)
            {
                ViewBag.ProductId = product.Id;
                ViewBag.ProductName = product.Name;
                return View(model);
            }

            var existingSpecs = await _context.ProductSpecs
                .FirstOrDefaultAsync(s => s.ProductId == id);

            if (existingSpecs == null)
            {
                model.Id = 0;
                _context.ProductSpecs.Add(model);
            }
            else
            {
                model.Id = existingSpecs.Id;
                _context.Entry(existingSpecs).CurrentValues.SetValues(model);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cấu hình sản phẩm đã được cập nhật thành công!";

            return RedirectToAction("Index", "ProductSpecs", new { area = "Admin" });
        }

        // ===========================
        // OPTIONAL: Xem nhanh cấu hình (read-only)
        // /Admin/ProductSpecs/Details/5  (5 = productId)
        // ===========================
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        public async Task<IActionResult> Details(int id) // id = ProductId
        {
            var specs = await _context.ProductSpecs
                .Include(s => s.Product)
                .FirstOrDefaultAsync(s => s.ProductId == id);

            if (specs == null)
                return NotFound();

            return View(specs);
        }
    }
}
