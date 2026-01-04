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
            const int pageSize = 5;
            if (page < 1) page = 1;

            // Base query (NO paging)
            IQueryable<ProductRating> q = _context.ProductRatings
    .AsNoTracking()
    .Include(r => r.User)
    .Include(r => r.Product)
    .Include(r => r.Replies).ThenInclude(x => x.User)
    .Include(r => r.Reports).ThenInclude(x => x.User)
    .Include(r => r.Votes)
    .Include(r => r.Images);

            // Search
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

            // Total
            var totalCount = await q.CountAsync();

            // Paging
            var ratings = await q
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // ===== Verified Purchase: 1 query cho cả trang (tránh N+1) =====
            var userIds = ratings.Select(r => r.UserId).Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            var productIds = ratings.Select(r => r.ProductId).Distinct().ToList();

            // lấy các cặp (UserId, ProductId) đã mua & hoàn tất
            var verifiedPairs = await (
                from od in _context.OrderDetails.AsNoTracking()
                join o in _context.Orders.AsNoTracking() on od.OrderId equals o.Id
                where o.Status == OrderStatus.HoanTat
                      && userIds.Contains(o.UserId)
                      && productIds.Contains(od.ProductId)
                select new { o.UserId, od.ProductId }
            ).Distinct().ToListAsync();

            // HashSet để check O(1)
            var verifiedSet = new HashSet<string>(
                verifiedPairs.Select(x => $"{x.UserId}|{x.ProductId}")
            );

            foreach (var r in ratings)
            {
                r.IsVerifiedPurchase = verifiedSet.Contains($"{r.UserId}|{r.ProductId}");
            }

            // ===== Stats (AvgStars + StarCounts) =====
            // tính theo đúng filter hiện tại (khớp totalCount đang hiển thị)
            if (productId > 0)
            {
                // q đã include nhiều thứ; để stats nhẹ hơn, dùng query stats riêng (cùng điều kiện)
                IQueryable<ProductRating> statsQ = _context.ProductRatings.AsNoTracking();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim();
                    statsQ = statsQ
                        .Include(r => r.User)
                        .Include(r => r.Product)
                        .Where(r =>
                            (!string.IsNullOrEmpty(r.Comment) && r.Comment.Contains(s)) ||
                            (r.Product != null && r.Product.Name.Contains(s)) ||
                            (r.User != null && (
                                (!string.IsNullOrEmpty(r.User.FullName) && r.User.FullName.Contains(s)) ||
                                (!string.IsNullOrEmpty(r.User.UserName) && r.User.UserName.Contains(s))
                            ))
                        );
                }

                statsQ = statsQ.Where(r => r.ProductId == productId);
                if (star > 0) statsQ = statsQ.Where(r => r.Stars == star);

                var all = await statsQ.Select(r => r.Stars).ToListAsync();

                ViewBag.AvgStars = all.Any() ? all.Average() : 0.0;
                // list từ 5 -> 1
                ViewBag.StarCounts = Enumerable.Range(1, 5)
                    .Select(s => all.Count(x => x == s))
                    .Reverse()
                    .ToList();
            }

            // ViewBag cho View
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.Search = search;
            ViewBag.ProductId = productId;
            ViewBag.Star = star;
            ViewBag.Products = await _context.Products.AsNoTracking().OrderByDescending(p => p.Id).ToListAsync();

            return View(ratings);
        }

        // Thêm phản hồi
        [HttpPost]
        public async Task<IActionResult> AddReply(int ratingId, string content)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                TempData["ErrorMessage"] = "Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn.";
                return RedirectBackToIndex();
            }

            content = (content ?? "").Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Nội dung không được bỏ trống.";
                return RedirectBackToIndex();
            }

            var ratingExists = await _context.ProductRatings.AnyAsync(r => r.Id == ratingId);
            if (!ratingExists)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đánh giá để phản hồi.";
                return RedirectBackToIndex();
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
            return RedirectBackToIndex();
        }

        [HttpPost]
        public async Task<IActionResult> EditReply(int id, string content, string search = "", int productId = 0, int star = 0, int page = 1)
        {
            var reply = await _context.ProductRatingReplies.FindAsync(id);
            if (reply == null) return NotFound();

            content = (content ?? "").Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Nội dung không được bỏ trống.";
                return RedirectToAction("Index", new { search, productId, star, page });
            }

            reply.Content = content;
            reply.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật phản hồi thành công!";
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
            return RedirectBackToIndex();
        }

        // Helper: quay lại trang đang đứng để không mất filter/page
        private IActionResult RedirectBackToIndex()
        {
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrWhiteSpace(referer))
                return Redirect(referer);

            return RedirectToAction("Index");
        }
    }
}
