using Microsoft.AspNetCore.Mvc;
using WebBanDienThoai.Models;
using Microsoft.EntityFrameworkCore;

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
                .ToList();

            var suggestedProducts = _context.Products
                .OrderBy(p => Guid.NewGuid())
                .Take(6)
                .ToList();
            ViewBag.SuggestedProducts = suggestedProducts;

            return View(wishlistItems); // Trả về danh sách sản phẩm yêu thích
        }

        // Action để thêm sản phẩm vào wishlist
        [HttpPost]
        public async Task<IActionResult> AddToWishlist(int productId)
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.Identity.Name;

                // Kiểm tra xem sản phẩm đã có trong wishlist chưa
                var existingWishlistItem = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

                if (existingWishlistItem == null)
                {
                    // Thêm sản phẩm vào danh sách yêu thích
                    var wishlistItem = new Wishlist
                    {
                        UserId = userId,
                        ProductId = productId
                    };

                    _context.Wishlists.Add(wishlistItem);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Sản phẩm đã được thêm vào yêu thích!";
                }
                else
                {
                    TempData["SuccessMessage"] = "Sản phẩm đã có trong danh sách yêu thích.";
                }

                return RedirectToAction("Display", "Product", new { id = productId });
            }

            TempData["ErrorMessage"] = "Vui lòng đăng nhập để thêm sản phẩm vào yêu thích.";
            return RedirectToAction("Login", "Account");
        }
        [HttpPost]
        public async Task<IActionResult> RemoveFromWishlist(int productId)
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = User.Identity.Name;

                // Tìm sản phẩm trong wishlist của người dùng
                var wishlistItem = await _context.Wishlists
                    .FirstOrDefaultAsync(w => w.UserId == userId && w.ProductId == productId);

                if (wishlistItem != null)
                {
                    // Xóa sản phẩm khỏi wishlist
                    _context.Wishlists.Remove(wishlistItem);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Sản phẩm đã được xóa khỏi yêu thích!";
                }

                return RedirectToAction("Index", "Wishlist"); // Quay lại trang danh sách yêu thích
            }

            TempData["ErrorMessage"] = "Vui lòng đăng nhập để xóa sản phẩm khỏi yêu thích.";
            return RedirectToAction("Login", "Account"); // Chuyển đến trang đăng nhập nếu chưa đăng nhập
        }

    }
}
