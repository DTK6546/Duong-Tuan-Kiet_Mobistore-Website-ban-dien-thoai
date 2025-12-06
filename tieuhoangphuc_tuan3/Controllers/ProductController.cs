using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Extensions;
using WebBanDienThoai.Models;
using WebBanDienThoai.Repositories;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace WebBanDienThoai.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;


        public ProductController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IProductRepository productRepository, ICategoryRepository categoryRepository)
        {
            _context = context;
            _userManager = userManager;
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
        }

        // Index với tìm kiếm nhanh
        public async Task<IActionResult> Index(string query)
        {
            var products = await _productRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(query))
            {
                query = query.ToLower();
                products = products.Where(p => p.Name.ToLower().Contains(query)).ToList();
            }

            // Gán danh sách danh mục (bỏ danh mục "Chưa phân loại")
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = categories.Where(c => c.Id != 16).ToList();

            return View(products);
        }

        // Index với bộ lọc và phân trang
        [HttpGet]
        public async Task<IActionResult> Index(int pageNumber = 1, int? categoryId = null, int? subCategoryId = null, decimal? minPrice = null, decimal? maxPrice = null, string query = "", string sortBy = "")
        {
            int pageSize = 16;
            var products = await _context.Products
    .Include(p => p.Category)
    .Include(p => p.SubCategory)
    .Include(p => p.Variants)          // 👈 LẤY LUÔN BIẾN THỂ
    .ToListAsync();

            if (categoryId.HasValue && categoryId.Value != 0)
            {
                products = products.Where(p => p.CategoryId == categoryId.Value).ToList();
            }

            if (subCategoryId.HasValue && subCategoryId.Value != 0)
            {
                products = products.Where(p => p.SubCategoryId == subCategoryId.Value).ToList();
            }

            if (minPrice.HasValue)
            {
                products = products.Where(p => p.DiscountedPrice >= minPrice.Value).ToList();
            }

            if (maxPrice.HasValue)
            {
                products = products.Where(p => p.DiscountedPrice <= maxPrice.Value).ToList();
            }

            if (!string.IsNullOrEmpty(query))
            {
                products = products.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            switch (sortBy)
            {
                case "banchay":
                    products = products.OrderByDescending(p =>
                        _context.OrderDetails.Where(od => od.ProductId == p.Id && od.Order.Status == OrderStatus.HoanTat)
                                            .Sum(od => od.Quantity)
                    ).ToList();
                    break;
                case "giamgia":
                    products = products.OrderByDescending(p => p.DiscountPercent).ToList();
                    break;
                case "moi":
                    products = products.OrderByDescending(p => p.Id).ToList(); // hoặc CreatedAt
                    break;
                case "giathapcao":
                    products = products.OrderBy(p => p.DiscountedPrice).ToList();
                    break;
                case "giacaothap":
                    products = products.OrderByDescending(p => p.DiscountedPrice).ToList();
                    break;
            }

            var totalItems = products.Count();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            var pagedProducts = products
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Lấy danh sách ID sản phẩm hiển thị
            var productIds = pagedProducts.Select(p => p.Id).ToList();

            // Lấy số lượt bán mỗi sản phẩm (chỉ order Hoàn Tất)
            var soldDict = _context.OrderDetails
                .Where(od => productIds.Contains(od.ProductId) && od.Order.Status == OrderStatus.HoanTat)
                .GroupBy(od => od.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
                .ToDictionary(g => g.ProductId, g => g.Sold);

            // Chuyển về ViewModel (bổ sung SoldCount)
            var model = pagedProducts.Select(p => new ProductWithSoldCount
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                Description = p.Description,
                ImageUrl = p.ImageUrl,
                Images = p.Images,
                CategoryId = p.CategoryId,
                Category = p.Category,
                Rating = p.Rating,
                DiscountPercent = p.DiscountPercent,
                DiscountedPrice = p.DiscountedPrice,
                SubCategoryId = p.SubCategoryId,
                SubCategory = p.SubCategory,
                SoldCount = soldDict.ContainsKey(p.Id) ? soldDict[p.Id] : 0,
                Variants = p.Variants?.ToList() ?? new List<ProductVariant>()
            }).ToList();

            // Truyền ViewBag các kiểu như cũ (giữ nguyên code)
            ViewBag.PageNumber = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.Categories = (await _categoryRepository.GetAllAsync()).Where(c => c.Id != 16).ToList();
            ViewBag.CategoryId = categoryId ?? 0;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Query = query;
            var allSubCategories = categoryId.HasValue && categoryId.Value != 0
                ? _context.SubCategories.Where(s => s.CategoryId == categoryId.Value).ToList()
                : _context.SubCategories
                    .GroupBy(s => s.Name)
                    .Select(g => g.First())
                    .ToList();
            ViewBag.SubCategories = allSubCategories;
            ViewBag.SubCategoryId = subCategoryId ?? 0;
            ViewBag.SortBy = sortBy;

            // Trả model mới
            return View(model);
        }


        [Authorize]
        public async Task<IActionResult> Buy(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);
            if (cartItem != null)
            {
                cartItem.Quantity += 1;
            }
            else
            {
                cart.Items.Add(new CartItem
                {
                    ProductId = product.Id,
                    Name = product.Name,
                    Price = product.DiscountedPrice,
                    Quantity = 1,
                    ImageUrl = product.ImageUrl
                });
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return RedirectToAction("Index", "ShoppingCart");
        }

        private async Task<string> SaveImage(IFormFile image)
        {
            var savePath = Path.Combine("wwwroot/images", image.FileName);
            using (var fileStream = new FileStream(savePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return "/images/" + image.FileName;
        }

        public async Task<IActionResult> Display(int id, int? variantId)
        {
            // Lấy product + navigation cần thiết
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Images)   // Ảnh phụ
                .Include(p => p.Specs)    // Thông số kỹ thuật
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null)
                return NotFound();

            // Đảm bảo luôn có list ảnh
            if (product.Images == null)
                product.Images = new List<ProductImage>();

            // ===== ĐÁNH GIÁ =====
            var ratings = await _context.ProductRatings
                .Include(r => r.User)
                .Include(r => r.Replies).ThenInclude(reply => reply.User)
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            ViewBag.Ratings = ratings;

            ProductRating myRating = null;
            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                myRating = await _context.ProductRatings
                    .FirstOrDefaultAsync(r => r.ProductId == id && r.UserId == user.Id);
            }
            ViewBag.MyRating = myRating;

            // ===== ĐÃ BÁN =====
            int soldCount = await _context.OrderDetails
                .Where(od => od.ProductId == id && od.Order.Status == OrderStatus.HoanTat)
                .SumAsync(od => (int?)od.Quantity) ?? 0;

            // ===== BIẾN THỂ (màu / dung lượng) =====
            var variants = await _context.ProductVariants
                .Where(v => v.ProductId == id && v.Stock > 0)
                .ToListAsync();

            ViewBag.Variants = variants;

            ProductVariant selectedVariant = null;
            if (variants.Any())
            {
                if (variantId.HasValue)
                {
                    selectedVariant = variants.FirstOrDefault(v => v.Id == variantId.Value);
                }

                if (selectedVariant == null)
                {
                    selectedVariant = variants.First();
                }
            }
            ViewBag.SelectedVariant = selectedVariant;

            // ===== MODEL CHÍNH CHO VIEW =====
            var displayModel = new ProductWithSoldCount
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                Description = product.Description,
                ImageUrl = product.ImageUrl,
                Images = product.Images,
                CategoryId = product.CategoryId,
                Category = product.Category,
                Rating = product.Rating,
                DiscountPercent = product.DiscountPercent,
                DiscountedPrice = product.DiscountedPrice,
                SubCategoryId = product.SubCategoryId,
                SubCategory = product.SubCategory,
                SoldCount = soldCount,
                Specs = product.Specs,
                ServiceCommitment = product.ServiceCommitment,
                Variants = variants
            };

            // =====================================================================
            //                    SẢN PHẨM LIÊN QUAN
            // =====================================================================

            var relatedEntities = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Where(p => p.Id != id && p.SubCategoryId == product.SubCategoryId)
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToListAsync();

            // =====================================================================
            //                    SẢN PHẨM ĐÃ XEM (COOKIE)
            // =====================================================================

            const string viewedCookieName = "RecentlyViewedProducts";
            List<int> viewedIds = new List<int>();

            if (Request.Cookies.TryGetValue(viewedCookieName, out var cookieValue)
                && !string.IsNullOrEmpty(cookieValue))
            {
                try
                {
                    viewedIds = JsonSerializer.Deserialize<List<int>>(cookieValue) ?? new List<int>();
                }
                catch
                {
                    viewedIds = new List<int>();
                }
            }

            // Cập nhật danh sách id đã xem
            viewedIds.Remove(id);
            viewedIds.Insert(0, id); // đưa sản phẩm hiện tại lên đầu

            if (viewedIds.Count > 10)
                viewedIds = viewedIds.Take(10).ToList();

            // Lưu lại cookie
            Response.Cookies.Append(
                viewedCookieName,
                JsonSerializer.Serialize(viewedIds),
                new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(7),
                    HttpOnly = false,
                    IsEssential = true
                });

            // Lấy danh sách sản phẩm đã xem (trừ sản phẩm hiện tại)
            var viewedEntities = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Images)
                .Include(p => p.Variants)
                .Where(p => viewedIds.Contains(p.Id) && p.Id != id)
                .ToListAsync();

            // =====================================================================
            //             TÍNH SOLD COUNT CHO RELATED + VIEWED
            // =====================================================================

            var allIds = relatedEntities.Select(p => p.Id)
                .Concat(viewedEntities.Select(p => p.Id))
                .Distinct()
                .ToList();

            var soldDict = new Dictionary<int, int>();

            if (allIds.Any())
            {
                soldDict = await _context.OrderDetails
                    .Where(od => allIds.Contains(od.ProductId) && od.Order.Status == OrderStatus.HoanTat)
                    .GroupBy(od => od.ProductId)
                    .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
                    .ToDictionaryAsync(g => g.ProductId, g => g.Sold);
            }

            // Hàm map Product -> ProductWithSoldCount để dùng cho _ProductCard
            ProductWithSoldCount MapToVm(Product p) => new ProductWithSoldCount
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                Description = p.Description,
                ImageUrl = p.ImageUrl,
                Images = p.Images,
                CategoryId = p.CategoryId,
                Category = p.Category,
                Rating = p.Rating,
                DiscountPercent = p.DiscountPercent,
                DiscountedPrice = p.DiscountedPrice,
                SubCategoryId = p.SubCategoryId,
                SubCategory = p.SubCategory,
                SoldCount = soldDict.ContainsKey(p.Id) ? soldDict[p.Id] : 0,
                Variants = p.Variants?.ToList() ?? new List<ProductVariant>()
            };

            ViewBag.RelatedProducts = relatedEntities.Select(MapToVm).ToList();
            ViewBag.ViewedProducts = viewedEntities.Select(MapToVm).ToList();

            // =====================================================================

            return View(displayModel);
        }


        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return RedirectToAction("Index", "Product", new { area = "Admin" });
        }

        [HttpPost, ActionName("DeleteConfirmed")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            await _productRepository.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmitRating(int productId, int stars, string comment)
        {
            if (stars < 1 || stars > 5)
                return BadRequest("Điểm phải từ 1 đến 5 sao.");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // Kiểm tra đã mua hàng và đã nhận (trạng thái giao hàng thành công)
            var hasPurchased = await _context.Orders
                .Include(o => o.OrderDetails)
                .AnyAsync(o => o.UserId == user.Id
                    && o.OrderDetails.Any(od => od.ProductId == productId)
                    && o.Status == OrderStatus.HoanTat);

            if (!hasPurchased)
            {
                TempData["ErrorMessage"] = "Bạn cần mua và nhận hàng thành công mới được đánh giá sản phẩm này.";
                return RedirectToAction("Display", new { id = productId });
            }

            // Kiểm tra đã từng đánh giá chưa
            var existing = await _context.ProductRatings
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == user.Id);

            if (existing == null)
            {
                // Thêm mới đánh giá
                existing = new ProductRating
                {
                    ProductId = productId,
                    UserId = user.Id,
                    Stars = stars,
                    Comment = comment,
                    CreatedAt = DateTime.Now
                };
                _context.ProductRatings.Add(existing);
            }
            else
            {
                // Cập nhật đánh giá
                existing.Stars = stars;
                existing.Comment = comment;
                existing.UpdatedAt = DateTime.Now;
                _context.ProductRatings.Update(existing);
            }

            // Lưu thay đổi
            await _context.SaveChangesAsync();

            // Cập nhật trung bình rating cho Product
            var allRatings = await _context.ProductRatings.Where(r => r.ProductId == productId).ToListAsync();
            var avgStars = allRatings.Any() ? allRatings.Average(r => r.Stars) : 0;
            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                product.Rating = avgStars; // double
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "Đánh giá của bạn đã được ghi nhận!";
            return RedirectToAction("Display", new { id = productId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Employer")]
        public async Task<IActionResult> ReplyRating(int productRatingId, string content)
        {
            var user = await _userManager.GetUserAsync(User);
            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Nội dung phản hồi không được để trống!";
                var productId = (await _context.ProductRatings.FindAsync(productRatingId)).ProductId;
                return RedirectToAction("Display", new { id = productId });
            }

            var reply = new ProductRatingReply
            {
                ProductRatingId = productRatingId,
                Content = content,
                UserId = user.Id,
                CreatedAt = DateTime.Now
            };
            _context.ProductRatingReplies.Add(reply);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã gửi phản hồi cho đánh giá!";
            var pId = (await _context.ProductRatings.FindAsync(productRatingId)).ProductId;
            return RedirectToAction("Display", new { id = pId });
        }

    }
}
