using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // Đảm bảo chỉ Admin mới vào được
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public BlogController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // 📊 1. Quản lý danh sách bài viết
        public async Task<IActionResult> Index()
        {
            var blogPosts = await _context.BlogPosts
                .Include(b => b.BlogCategory)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
            return View(blogPosts);
        }

        // ➕ 2. Giao diện Thêm mới bài viết (GET)
        public async Task<IActionResult> Create()
        {
            var categories = await _context.BlogCategories.ToListAsync();
            ViewBag.BlogCategoryId = new SelectList(categories, "Id", "Name");
            return View();
        }

        // 💾 3. Xử lý Thêm mới bài viết (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BlogPost blogPost, IFormFile? thumbnailFile)
        {
            if (ModelState.IsValid)
            {
                // Xử lý Upload ảnh đại diện bài viết
                if (thumbnailFile != null && thumbnailFile.Length > 0)
                {
                    blogPost.ThumbnailUrl = await SaveBlogImage(thumbnailFile);
                }

                // Tự động chuẩn hóa link YouTube nếu Admin copy nhầm link thường
                blogPost.VideoEmbedUrl = ConvertToEmbedUrl(blogPost.VideoEmbedUrl);
                blogPost.CreatedAt = DateTime.Now;

                _context.BlogPosts.Add(blogPost);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đăng bài viết mới thành công!";
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.BlogCategories.ToListAsync();
            ViewBag.BlogCategoryId = new SelectList(categories, "Id", "Name", blogPost.BlogCategoryId);
            return View(blogPost);
        }

        // ✏️ 4. Giao diện Chỉnh sửa bài viết (GET)
        public async Task<IActionResult> Edit(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return NotFound();

            var categories = await _context.BlogCategories.ToListAsync();
            ViewBag.BlogCategoryId = new SelectList(categories, "Id", "Name", post.BlogCategoryId);
            return View(post);
        }

        // 💾 5. Xử lý Cập nhật bài viết (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BlogPost blogPost, IFormFile? thumbnailFile)
        {
            if (id != blogPost.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existingPost = await _context.BlogPosts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
                    if (existingPost == null) return NotFound();

                    if (thumbnailFile != null && thumbnailFile.Length > 0)
                    {
                        // Xóa ảnh cũ nếu có để tránh rác source code
                        if (!string.IsNullOrEmpty(existingPost.ThumbnailUrl))
                        {
                            DeleteOldImage(existingPost.ThumbnailUrl);
                        }
                        blogPost.ThumbnailUrl = await SaveBlogImage(thumbnailFile);
                    }
                    else
                    {
                        blogPost.ThumbnailUrl = existingPost.ThumbnailUrl;
                    }

                    blogPost.VideoEmbedUrl = ConvertToEmbedUrl(blogPost.VideoEmbedUrl);
                    blogPost.CreatedAt = existingPost.CreatedAt; // Giữ nguyên ngày tạo gốc
                    blogPost.UpdatedAt = DateTime.Now;

                    _context.BlogPosts.Update(blogPost);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Cập nhật bài viết thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BlogPostExists(blogPost.Id)) return NotFound();
                    throw;
                }
                return RedirectToAction(nameof(Index));
            }

            var categories = await _context.BlogCategories.ToListAsync();
            ViewBag.BlogCategoryId = new SelectList(categories, "Id", "Name", blogPost.BlogCategoryId);
            return View(blogPost);
        }

        // ❌ 6. Xử lý Xóa nhanh bài viết qua Ajax hoặc Post thường
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var post = await _context.BlogPosts.FindAsync(id);
            if (post == null) return Json(new { success = false, message = "Không tìm thấy bài viết" });

            if (!string.IsNullOrEmpty(post.ThumbnailUrl))
            {
                DeleteOldImage(post.ThumbnailUrl);
            }

            _context.BlogPosts.Remove(post);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa bài viết thành công!" });
        }

        // 🛠️ HÀM PHỤ 1: Chuẩn hóa link YouTube sang link nhúng (Embed) độc quyền
        private string? ConvertToEmbedUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;

            // Nếu đã là link nhúng embed chuẩn thì bỏ qua
            if (url.Contains("youtube.com/embed/")) return url;

            // Bẫy regex nhận diện ID của video Youtube từ mọi định dạng link (watch?v=, share, short...)
            var regex = new Regex(@"(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=))([\w-]{11})");
            var match = regex.Match(url);

            if (match.Success)
            {
                string videoId = match.Groups[1].Value;
                return $"https://www.youtube.com/embed/{videoId}";
            }

            return url; // Nếu chịu chết không bóc được thì giữ nguyên link gốc của Admin nhập
        }

        // 🛠️ HÀM PHỤ 2: Lưu tệp ảnh đại diện vào thư mục wwwroot/images/blog
        private async Task<string> SaveBlogImage(IFormFile file)
        {
            string uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "images/blog");
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
            string filePath = Path.Combine(uploadDir, fileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return $"/images/blog/{fileName}";
        }

        // 🛠️ HÀM PHỤ 3: Xóa file ảnh vật lý trên Server
        private void DeleteOldImage(string relativePath)
        {
            string fullPath = Path.Combine(_webHostEnvironment.WebRootPath, relativePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }

        private bool BlogPostExists(int id)
        {
            return _context.BlogPosts.Any(e => e.Id == id);
        }
    }
}