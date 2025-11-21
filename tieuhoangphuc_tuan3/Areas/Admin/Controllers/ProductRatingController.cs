using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Employer")]
    public class ProductRatingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProductRatingController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Danh sách tất cả đánh giá và phản hồi
        public async Task<IActionResult> Index(string search = "", int productId = 0, int star = 0, int page = 1)
        {
            int pageSize = 5;
            var query = _context.ProductRatings
                .Include(r => r.User)
                .Include(r => r.Product)
                .Include(r => r.Replies).ThenInclude(x => x.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(r =>
                    r.Comment.Contains(search) ||
                    r.Product.Name.Contains(search) ||
                    r.User.FullName.Contains(search) ||
                    r.User.UserName.Contains(search)
                );
            if (productId > 0) query = query.Where(r => r.ProductId == productId);
            if (star > 0) query = query.Where(r => r.Stars == star);

            var totalCount = await query.CountAsync();
            var ratings = await query.OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.Search = search;
            ViewBag.ProductId = productId;
            ViewBag.Star = star;
            ViewBag.Products = await _context.Products.ToListAsync();

            return View(ratings);
        }

        // Thêm phản hồi (Hiện form hoặc trả về PartialView nếu dùng AJAX)
        [HttpGet]
        public IActionResult AddReply(int ratingId)
        {
            ViewBag.RatingId = ratingId;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddReply(int ratingId, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (string.IsNullOrWhiteSpace(content) || user == null)
            {
                TempData["ErrorMessage"] = "Nội dung không được bỏ trống";
                return RedirectToAction("Index");
            }

            var reply = new ProductRatingReply
            {
                ProductRatingId = ratingId,
                Content = content,
                UserId = user.Id,
                CreatedAt = DateTime.Now
            };
            _context.ProductRatingReplies.Add(reply);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Phản hồi đã được gửi!";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> EditReply(int id, string content, string search = "", int productId = 0, int star = 0, int page = 1)
        {
            var reply = await _context.ProductRatingReplies.FindAsync(id);
            if (reply == null) return NotFound();
            reply.Content = content;
            reply.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật phản hồi thành công!";
            // Trả về Index với các filter cũ (giúp user không mất trang)
            return RedirectToAction("Index", new { search, productId, star, page });
        }


        // Xóa phản hồi
        [HttpPost]
        public async Task<IActionResult> DeleteReply(int id)
        {
            var reply = await _context.ProductRatingReplies.FindAsync(id);
            if (reply == null) return NotFound();
            _context.ProductRatingReplies.Remove(reply);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa phản hồi!";
            return RedirectToAction("Index");
        }
    }
}
