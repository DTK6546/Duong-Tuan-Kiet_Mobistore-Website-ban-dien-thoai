using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Employer")]
    public class ContentModerationController : Controller
    {
        private readonly ApplicationDbContext _db;

        public ContentModerationController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Trang quản lý duyệt bình luận và danh sách người dùng
        public IActionResult Index()
        {
            // Lấy danh sách các đánh giá chưa được duyệt hoặc toàn bộ để quản lý
            var ratings = _db.ProductRatings
                .Include(r => r.Product)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            ViewBag.Users = _db.ApplicationUsers.OrderBy(u => u.UserName).ToList();
            return View(ratings);
        }

        // Chức năng 1: Phê duyệt bình luận hợp lệ
        [HttpPost]
        public IActionResult ApproveComment(int id)
        {
            var rating = _db.ProductRatings.FirstOrDefault(r => r.Id == id);
            if (rating != null)
            {
                rating.IsApproved = true;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Đã phê duyệt hiển thị bình luận thành công!";
            }
            return RedirectToAction(nameof(Index));
        }

        // Chức năng 1: Ẩn/Từ chối bình luận xấu
        [HttpPost]
        public IActionResult HideComment(int id)
        {
            var rating = _db.ProductRatings.FirstOrDefault(r => r.Id == id);
            if (rating != null)
            {
                rating.IsApproved = false;
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Đã ẩn bình luận vi phạm khỏi giao diện bách hóa!";
            }
            return RedirectToAction(nameof(Index));
        }

        // Chức năng 2: Chặn / Khóa tài khoản Spammer bài viết
        [HttpPost]
        public IActionResult ToggleBanUser(string userId)
        {
            var user = _db.ApplicationUsers.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.IsBanned = !user.IsBanned; // Đảo trạng thái Ban/Unban
                _db.SaveChanges();
                TempData["SuccessMessage"] = user.IsBanned
                    ? $"Đã thực hiện BLOCK tài khoản {user.FullName} thành công!"
                    : $"Đã mở khóa bãi bỏ lệnh cấm cho tài khoản {user.FullName}!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}