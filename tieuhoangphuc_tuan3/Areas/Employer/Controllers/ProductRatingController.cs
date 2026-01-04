using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Employer.Controllers
{
    [Area("Employer")]
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

        // Danh sách đánh giá + phản hồi
        public async Task<IActionResult> Index(string search = "", int productId = 0, int star = 0, int page = 1)
        {
            const int pageSize = 5;
            if (page < 1) page = 1;

            IQueryable<ProductRating> q = _context.ProductRatings
    .AsNoTracking()
    .Include(r => r.User)
    .Include(r => r.Product)
    .Include(r => r.Replies).ThenInclude(x => x.User)
    .Include(r => r.Reports).ThenInclude(x => x.User)
    .Include(r => r.Votes)
    .Include(r => r.Images);

            // Search (chống null)
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                q = q.Where(r =>
                    (!string.IsNullOrEmpty(r.Comment) && r.Comment.Contains(search)) ||
                    (r.Product != null && r.Product.Name.Contains(search)) ||
                    (r.User != null && (
                        (!string.IsNullOrEmpty(r.User.FullName) && r.User.FullName.Contains(search)) ||
                        (!string.IsNullOrEmpty(r.User.UserName) && r.User.UserName.Contains(search))
                    ))
                );
            }

            // Filters
            if (productId > 0) q = q.Where(r => r.ProductId == productId);
            if (star > 0) q = q.Where(r => r.Stars == star);

            var totalCount = await q.CountAsync();

            var ratings = await q
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ✅ Verified Purchase (1 query cho cả trang)
            var userIds = ratings.Select(r => r.UserId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            var productIds = ratings.Select(r => r.ProductId).Distinct().ToList();

            if (userIds.Count > 0 && productIds.Count > 0)
            {
                var verifiedPairs = await (
                    from od in _context.OrderDetails.AsNoTracking()
                    join o in _context.Orders.AsNoTracking() on od.OrderId equals o.Id
                    where o.Status == OrderStatus.HoanTat
                          && userIds.Contains(o.UserId)
                          && productIds.Contains(od.ProductId)
                    select new { o.UserId, od.ProductId }
                ).Distinct().ToListAsync();

                var verifiedSet = new HashSet<string>(verifiedPairs.Select(x => $"{x.UserId}|{x.ProductId}"));

                foreach (var r in ratings)
                    r.IsVerifiedPurchase = verifiedSet.Contains($"{r.UserId}|{r.ProductId}");
            }
            else
            {
                foreach (var r in ratings)
                    r.IsVerifiedPurchase = false;
            }

            // ViewBag
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.Search = search;
            ViewBag.ProductId = productId;
            ViewBag.Star = star;

            ViewBag.Products = await _context.Products
                .AsNoTracking()
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            return View(ratings);
        }

        // Add reply (POST) - giữ filter/page
        [HttpPost]
        public async Task<IActionResult> AddReply(int ratingId, string content, string search = "", int productId = 0, int star = 0, int page = 1)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập lại.";
                return RedirectToAction("Index", new { search, productId, star, page });
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Nội dung không được bỏ trống.";
                return RedirectToAction("Index", new { search, productId, star, page });
            }

            // rating tồn tại?
            var ratingExists = await _context.ProductRatings.AsNoTracking().AnyAsync(x => x.Id == ratingId);
            if (!ratingExists)
            {
                TempData["ErrorMessage"] = "Đánh giá không tồn tại.";
                return RedirectToAction("Index", new { search, productId, star, page });
            }

            var reply = new ProductRatingReply
            {
                ProductRatingId = ratingId,
                Content = content.Trim(),
                UserId = user.Id,
                CreatedAt = DateTime.Now
            };

            _context.ProductRatingReplies.Add(reply);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Phản hồi đã được gửi!";
            return RedirectToAction("Index", new { search, productId, star, page });
        }

        // Edit reply - giữ filter/page
        [HttpPost]
        public async Task<IActionResult> EditReply(int id, string content, string search = "", int productId = 0, int star = 0, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Nội dung không được bỏ trống.";
                return RedirectToAction("Index", new { search, productId, star, page });
            }

            var reply = await _context.ProductRatingReplies.FindAsync(id);
            if (reply == null)
            {
                TempData["ErrorMessage"] = "Phản hồi không tồn tại.";
                return RedirectToAction("Index", new { search, productId, star, page });
            }

            reply.Content = content.Trim();
            reply.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật phản hồi thành công!";
            return RedirectToAction("Index", new { search, productId, star, page });
        }

        // Delete reply - giữ filter/page
        [HttpPost]
        public async Task<IActionResult> DeleteReply(int id, string search = "", int productId = 0, int star = 0, int page = 1)
        {
            var reply = await _context.ProductRatingReplies.FindAsync(id);
            if (reply == null)
            {
                TempData["ErrorMessage"] = "Phản hồi không tồn tại.";
                return RedirectToAction("Index", new { search, productId, star, page });
            }

            _context.ProductRatingReplies.Remove(reply);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xóa phản hồi!";
            return RedirectToAction("Index", new { search, productId, star, page });
        }
    }
}
