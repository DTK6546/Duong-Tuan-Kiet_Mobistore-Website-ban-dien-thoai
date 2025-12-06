using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Extensions;
using WebBanDienThoai.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebBanDienThoai.Controllers
{
    public class CompareController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string CompareSessionKey = "CompareList";

        public CompareController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ====== Helpers ======
        // Lưu danh sách so sánh dạng ProductId + VariantId
        private List<CompareItem> GetCompareList()
        {
            return HttpContext.Session.GetObjectFromJson<List<CompareItem>>(CompareSessionKey)
                   ?? new List<CompareItem>();
        }

        private void SaveCompareList(List<CompareItem> items)
        {
            HttpContext.Session.SetObjectAsJson(CompareSessionKey, items);
        }

        // ====== Thêm 1 sản phẩm (và biến thể) vào danh sách so sánh ======
        // GET: /Compare/Add?productId=1&variantId=10
        [HttpGet]
        public async Task<IActionResult> Add(int productId, int? variantId, string? returnUrl)
        {
            // kiểm tra sản phẩm có tồn tại không
            var exists = await _context.Products.AnyAsync(p => p.Id == productId);
            if (!exists) return NotFound();

            var list = GetCompareList();

            // tìm xem product này đã có trong danh sách chưa
            var existing = list.FirstOrDefault(x => x.ProductId == productId);

            if (existing == null)
            {
                // giới hạn max 3
                if (list.Count >= 3)
                    list.RemoveAt(0);

                list.Add(new CompareItem
                {
                    ProductId = productId,
                    VariantId = variantId
                });
            }
            else
            {
                // đã có sản phẩm trong compare -> chỉ cập nhật variant
                existing.VariantId = variantId;
            }

            SaveCompareList(list);

            // Nếu gọi bằng AJAX
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, count = list.Count });

            // Nếu có returnUrl hợp lệ thì quay lại, còn không thì sang Select
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Select");
        }

        // ====== Xoá 1 sản phẩm khỏi danh sách so sánh ======
        [HttpPost]
        public IActionResult Remove(int productId)
        {
            var list = GetCompareList();
            var item = list.FirstOrDefault(x => x.ProductId == productId);
            if (item != null)
            {
                list.Remove(item);
                SaveCompareList(list);
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, count = list.Count });

            return RedirectToAction("Select");
        }

        // ====== Xoá tất cả sản phẩm khỏi danh sách so sánh ======
        [HttpPost]
        public IActionResult Clear()
        {
            SaveCompareList(new List<CompareItem>());

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });

            return RedirectToAction("Select");
        }

        // ====== Màn hình chọn tối đa 3 sản phẩm (giống hình 2) ======
        // GET: /Compare/Select
        public async Task<IActionResult> Select()
        {
            var compareItems = GetCompareList();           // List<CompareItem> { ProductId, VariantId }

            // nếu chưa có gì thì cho về trang sản phẩm
            if (!compareItems.Any())
            {
                return RedirectToAction("Index", "Product");
            }

            var productIds = compareItems.Select(x => x.ProductId).ToList();

            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Variants)
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            // Giữ đúng thứ tự theo session
            products = products.OrderBy(p => productIds.IndexOf(p.Id)).ToList();

            // Map sang CompareProductViewModel: Product + Variant đã chọn
            var model = products.Select(p =>
            {
                var item = compareItems.First(ci => ci.ProductId == p.Id);
                var selectedVariant = p.Variants?.FirstOrDefault(v => v.Id == item.VariantId);

                return new CompareProductViewModel
                {
                    Product = p,
                    Variant = selectedVariant
                };
            }).ToList();

            return View(model);
        }

        // ====== Trang so sánh chi tiết ======
        // GET: /Compare/Index
        public async Task<IActionResult> Index()
        {
            var compareItems = GetCompareList();
            if (!compareItems.Any())
                return RedirectToAction("Select");

            var productIds = compareItems.Select(x => x.ProductId).ToList();

            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Specs)
                .Include(p => p.Variants)
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync();

            // Giữ đúng thứ tự người dùng đã thêm
            products = products.OrderBy(p => productIds.IndexOf(p.Id)).ToList();

            // Map sang ViewModel: mỗi sản phẩm + biến thể được chọn (nếu có)
            var model = products.Select(p =>
            {
                var item = compareItems.First(x => x.ProductId == p.Id);
                var selectedVariant = p.Variants?.FirstOrDefault(v => v.Id == item.VariantId);

                return new CompareProductViewModel
                {
                    Product = p,
                    Variant = selectedVariant
                };
            }).ToList();

            return View(model);
        }
    }
}
