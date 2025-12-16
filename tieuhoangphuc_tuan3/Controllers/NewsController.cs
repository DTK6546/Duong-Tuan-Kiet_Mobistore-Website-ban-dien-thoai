using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebBanDienThoai.Extensions;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Controllers
{
    public class NewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NewsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /News
        public async Task<IActionResult> Index()
        {
            var news = await _context.News
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(news);
        }

        // GET: /News/Detail/5
        public async Task<IActionResult> Detail(int id)
        {
            var news = await _context.News
        .Include(n => n.Coupon) // ⭐ load luôn Coupon nếu có
        .FirstOrDefaultAsync(n => n.Id == id);

            if (news == null) return NotFound();

            var now = DateTime.Now;

            bool hasCoupon = false;          // có gắn coupon không (và còn trong chương trình)
            bool hasUsed = false;            // user đã dùng mã này trong đơn hàng chưa
            int remaining = 0;               // số lượt còn lại (Quantity - CurrentUsage)

            var coupon = news.Coupon;

            if (coupon != null)
            {
                remaining = coupon.Quantity - coupon.CurrentUsage;
                if (remaining < 0) remaining = 0;

                // Còn hiệu lực & còn lượt
                hasCoupon = coupon.IsActive &&
                            coupon.StartDate <= now &&
                            coupon.EndDate >= now &&
                            remaining > 0;

                // Nếu user đã đăng nhập thì kiểm tra đã dùng chưa
                if (User.Identity.IsAuthenticated)
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user != null)
                    {
                        hasUsed = await _context.CouponUsages
                            .AnyAsync(x => x.CouponId == coupon.Id && x.UserId == user.Id);
                    }
                }
            }

            ViewBag.HasCoupon = hasCoupon;    // để biết có hiển thị block khuyến mãi hay không
            ViewBag.HasUsed = hasUsed;      // đã dùng mã này chưa
            ViewBag.Remaining = remaining;    // còn lại bao nhiêu mã
            ViewBag.CouponAlive = coupon != null && coupon.IsActive &&
                                  coupon.StartDate <= now &&
                                  coupon.EndDate >= now;

            return View(news);
        }
    }
}
