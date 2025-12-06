using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using WebBanDienThoai;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProductVariantController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductVariantController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/ProductVariant?productId=5
        public async Task<IActionResult> Index(int productId)
        {
            var product = await _context.Products
                .Include(p => p.Variants)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.ProductId = product.Id;
            ViewBag.ProductName = product.Name;

            var variants = product.Variants
                                  .OrderBy(v => v.Color)
                                  .ThenBy(v => v.Storage)
                                  .ToList();

            return View(variants);
        }

        // GET: /Admin/ProductVariant/Create?productId=5
        public async Task<IActionResult> Create(int productId)
        {
            // 🔸 Nếu muốn lấy sẵn RAM / StorageAvailable từ ProductSpecs:
            var product = await _context.Products
                .Include(p => p.Specs)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                return NotFound();
            }

            ViewBag.ProductId = product.Id;
            ViewBag.ProductName = product.Name;

            var model = new ProductVariant
            {
                ProductId = productId,
                Stock = 0,
                // Gợi ý mặc định
                Ram = product.Specs?.Ram,
                StorageAvailable = product.Specs?.StorageAvailable
                // Storage (dung lượng) admin sẽ gõ 256GB/512GB cho từng biến thể
            };

            return View(model);
        }

        // POST: /Admin/ProductVariant/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductVariant model)
        {
            if (!ModelState.IsValid)
            {
                var product = await _context.Products.FindAsync(model.ProductId);
                if (product != null)
                {
                    ViewBag.ProductId = product.Id;
                    ViewBag.ProductName = product.Name;
                }

                return View(model);
            }

            _context.ProductVariants.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { productId = model.ProductId });
        }

        // GET: /Admin/ProductVariant/Edit/10
        public async Task<IActionResult> Edit(int id)
        {
            var variant = await _context.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (variant == null)
            {
                return NotFound();
            }

            ViewBag.ProductId = variant.ProductId;
            ViewBag.ProductName = variant.Product?.Name;

            return View(variant);
        }

        // POST: /Admin/ProductVariant/Edit/10
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductVariant model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                var product = await _context.Products.FindAsync(model.ProductId);
                if (product != null)
                {
                    ViewBag.ProductId = product.Id;
                    ViewBag.ProductName = product.Name;
                }

                return View(model);
            }

            try
            {
                _context.Update(model);   // sẽ tự update luôn Ram + StorageAvailable
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                bool exists = await _context.ProductVariants.AnyAsync(v => v.Id == model.Id);
                if (!exists)
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToAction(nameof(Index), new { productId = model.ProductId });
        }

        // POST: /Admin/ProductVariant/Delete/10
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var variant = await _context.ProductVariants.FindAsync(id);
            if (variant == null)
            {
                return NotFound();
            }

            int productId = variant.ProductId;

            _context.ProductVariants.Remove(variant);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { productId });
        }
    }
}
