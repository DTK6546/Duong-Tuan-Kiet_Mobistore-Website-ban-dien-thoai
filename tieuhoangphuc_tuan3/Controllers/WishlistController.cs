using Microsoft.AspNetCore.Mvc;
using WebBanDienThoai.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace WebBanDienThoai.Controllers
{
    public class WishlistController : Controller
    {
        private readonly ApplicationDbContext _context;

        public WishlistController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Action để hiển thị trang Wishlist (Danh sách yêu thích)
        public IActionResult Index()
        {
            var userId = User.Identity.Name; // Lấy thông tin người dùng
            var wishlistItems = _context.Wishlists
                .Where(w => w.UserId == userId)
                .Include(w => w.Product) // Bao gồm thông tin sản phẩm
                .Include(w => w.Variant)
                .ToList();

            var suggestedProducts = _context.Products
                .OrderBy(p => Guid.NewGuid())
                .Take(6)
                .ToList();
            ViewBag.SuggestedProducts = suggestedProducts;

            return View(wishlistItems); // Trả về danh sách sản phẩm yêu thích
        }

        // =========================================================================
        // 🔗 ACTION BỔ SUNG: TẠO LINK CHIA SẺ DANH SÁCH YÊU THÍCH TỪ USER ID
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> GenerateShareLink()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để thực hiện tính năng này." });
            }

            var userId = User.Identity.Name;

            // Kiểm tra xem danh sách có sản phẩm nào không trước khi tạo link
            var hasItems = await _context.Wishlists.AnyAsync(w => w.UserId == userId);
            if (!hasItems)
            {
                return Json(new { success = false, message = "Danh sách yêu thích của bạn đang trống, hãy thêm sản phẩm trước nhé!" });
            }

            // Mã hóa User.Identity.Name thành chuỗi Base64 an toàn làm Token chia sẻ công khai
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(userId);
            string sharingToken = Convert.ToBase64String(plainTextBytes);

            // Xây dựng đường dẫn URL tuyệt đối đến trang công khai
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var shareLink = $"{baseUrl}/Wishlist/Shared?token={sharingToken}";

            return Json(new { success = true, shareLink = shareLink });
        }

        // =========================================================================
        // 🌐 ACTION BỔ SUNG: TRANG XEM WISHLIST CHUNG (CÔNG KHAI CHO NGƯỜI ĐƯỢC CHIA SẺ)
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> Shared(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Index", "Home");
            }

            try
            {
                // Giải mã token ngược lại để lấy UserId gốc
                var base64EncodedBytes = Convert.FromBase64String(token);
                string targetUserId = System.Text.Encoding.UTF8.GetString(base64EncodedBytes);

                // Quét toàn bộ danh sách sản phẩm yêu thích của người dùng đó
                var sharedItems = await _context.Wishlists
                    .Where(w => w.UserId == targetUserId)
                    .Include(w => w.Product)
                    .Include(w => w.Variant)
                    .AsNoTracking()
                    .ToListAsync();

                // Gửi tên người chia sẻ ra giao diện (cắt chuỗi lấy phần trước @ nếu là email)
                string ownerName = targetUserId.Contains("@") ? targetUserId.Split('@')[0] : targetUserId;
                ViewData["OwnerName"] = ownerName;

                return View(sharedItems);
            }
            catch
            {
                return RedirectToAction("Index", "Home");
            }
        }

        // Action để thêm sản phẩm vào wishlist
        [HttpPost]
        public async Task<IActionResult> AddToWishlist(int productId, int? variantId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để thêm sản phẩm vào yêu thích.";
                return RedirectToAction("Login", "Account");
            }

            var userId = User.Identity.Name;

            // Kiểm tra trùng theo user + product + variant
            var existingWishlistItem = await _context.Wishlists
                .FirstOrDefaultAsync(w =>
                    w.UserId == userId &&
                    w.ProductId == productId &&
                    w.VariantId == variantId);

            if (existingWishlistItem == null)
            {
                var wishlistItem = new Wishlist
                {
                    UserId = userId,
                    ProductId = productId,
                    VariantId = variantId
                };

                _context.Wishlists.Add(wishlistItem);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Sản phẩm đã được thêm vào yêu thích!";
            }
            else
            {
                TempData["SuccessMessage"] = "Sản phẩm đã có trong danh sách yêu thích.";
            }

            // Nếu gọi AJAX thì trả về 200 OK
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Ok();

            return RedirectToAction("Display", "Product", new { id = productId });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveFromWishlist(int id)
        {
            if (!User.Identity.IsAuthenticated)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để xóa sản phẩm khỏi yêu thích.";
                return RedirectToAction("Login", "Account");
            }

            var userId = User.Identity.Name;

            var wishlistItem = await _context.Wishlists
                .FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId);

            if (wishlistItem != null)
            {
                _context.Wishlists.Remove(wishlistItem);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Sản phẩm đã được xóa khỏi yêu thích!";
            }

            return RedirectToAction("Index", "Wishlist");
        }
    }
}