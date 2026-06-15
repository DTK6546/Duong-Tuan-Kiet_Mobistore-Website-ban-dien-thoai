using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Controllers
{
    public class BlogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BlogController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // 📰 1. Trang danh sách bài viết (Có bộ lọc theo Category)
        [HttpGet]
        public async Task<IActionResult> Index(int? categoryId, string searchTerm)
        {
            IQueryable<BlogPost> query = _context.BlogPosts
                .Include(b => b.BlogCategory)
                .AsNoTracking();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                query = query.Where(b => b.BlogCategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(b => b.Title.Contains(searchTerm) || b.Summary.Contains(searchTerm));
            }

            var posts = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();

            ViewBag.Categories = await _context.BlogCategories.AsNoTracking().ToListAsync();
            ViewBag.SelectedCategory = categoryId;
            ViewBag.SearchTerm = searchTerm;

            return View(posts);
        }

        // 📖 2. Trang chi tiết bài viết (Xem nội dung + Nhúng Video + Danh sách Comment)
        [HttpGet("Blog/Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var post = await _context.BlogPosts
                .Include(b => b.BlogCategory)
                .Include(b => b.BlogComments!).ThenInclude(c => c.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (post == null) return NotFound();

            // Tăng viewcount khi có người đọc (Bẫy tương tác marketing)
            post.ViewCount++;
            _context.BlogPosts.Update(post);
            await _context.SaveChangesAsync();

            return View(post);
        }

        // 💬 3. AJAX / POST: Gửi bình luận thảo luận
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitComment(int blogPostId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Nội dung bình luận không được để trống!";
                return RedirectToAction(nameof(Details), new { id = blogPostId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Quét từ cấm (Content Moderation độc hại giống phần ProductRating của Kiệt)
            string checkContent = content.ToLower();
            string[] bannedWords = { "lừa đảo", "vcl", "dm", "đm", "fake" };
            if (bannedWords.Any(word => checkContent.Contains(word)))
            {
                TempData["ErrorMessage"] = "Bình luận chứa từ ngữ vi phạm tiêu chuẩn cộng đồng!";
                return RedirectToAction(nameof(Details), new { id = blogPostId });
            }

            var comment = new BlogComment
            {
                BlogPostId = blogPostId,
                UserId = user.Id,
                Content = content.Trim(),
                CreatedAt = DateTime.Now
            };

            _context.BlogComments.Add(comment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Bình luận của bạn đã được đăng thành công!";
            return RedirectToAction(nameof(Details), new { id = blogPostId });
        }
    }
}