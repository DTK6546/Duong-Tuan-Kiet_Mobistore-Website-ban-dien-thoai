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
                .Include(w => w.Variant)
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
