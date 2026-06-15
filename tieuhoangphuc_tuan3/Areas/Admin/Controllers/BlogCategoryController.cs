using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BlogCategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BlogCategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 📁 1. Hiển thị danh sách danh mục Blog
        public async Task<IActionResult> Index()
        {
            var categories = await _context.BlogCategories
                .AsNoTracking()
                .ToListAsync();
            return View(categories);
        }

        // ➕ 2. Thêm mới danh mục (POST thực hiện nhanh bằng form tại chỗ hoặc view)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogCategory category)
        {
            if (ModelState.IsValid)
            {
                // Tự động tạo slug đơn giản nếu cần
                category.Slug = category.Name.ToLower().Replace(" ", "-");

                _context.BlogCategories.Add(category);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm danh mục Blog mới thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // ✏️ 4. AJAX: Xử lý cập nhật tên danh mục nhanh từ Popup
        [HttpPost]
        public async Task<IActionResult> Edit(int id, string name)
        {
            var category = await _context.BlogCategories.FindAsync(id);
            if (category == null)
                return Json(new { success = false, message = "Không tìm thấy danh mục cần sửa!" });

            if (string.IsNullOrWhiteSpace(name))
                return Json(new { success = false, message = "Tên danh mục trống!" });

            category.Name = name.Trim();
            category.Slug = name.ToLower().Trim().Replace(" ", "-"); // Đồng bộ lại link SEO

            _context.BlogCategories.Update(category);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Cập nhật danh mục thành công!" });
        }

        // ❌ 3. Xóa nhanh danh mục Blog qua AJAX SweetAlert2
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var category = await _context.BlogCategories
                .Include(c => c.BlogPosts)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (category == null)
                return Json(new { success = false, message = "Không tìm thấy danh mục!" });

            // Bẫy chặn lỗi logic: Nếu danh mục đang có bài viết thì không cho xóa bừa bãi
            if (category.BlogPosts != null && category.BlogPosts.Any())
            {
                return Json(new { success = false, message = "Không thể xóa! Danh mục này đang chứa bài viết." });
            }

            _context.BlogCategories.Remove(category);
            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Xóa danh mục thành công!" });
        }
    }
}