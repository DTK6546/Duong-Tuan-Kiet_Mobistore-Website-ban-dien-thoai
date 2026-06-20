using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.Json;
using WebBanDienThoai.Extensions;
using WebBanDienThoai.Models;
using WebBanDienThoai.Repositories;

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

        public async Task<IActionResult> Index(string query)
        {
            var products = await _productRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(query))
            {
                query = query.ToLower();
                products = products.Where(p => p.Name.ToLower().Contains(query)).ToList();
            }

            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = categories.Where(c => c.Id != 16).ToList();

            var hotProductsData = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Variants)
                .Where(p => p.IsHot)
                .Take(4)
                .ToListAsync();

            var hotProductIds = hotProductsData.Select(p => p.Id).ToList();
            var hotSoldDict = await _context.OrderDetails
                .Where(od => hotProductIds.Contains(od.ProductId) && od.Order.Status == OrderStatus.HoanTat)
                .GroupBy(od => od.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(g => g.ProductId, g => g.Sold);

            ViewBag.HotProducts = hotProductsData.Select(p => new ProductWithSoldCount
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
                SoldCount = hotSoldDict.TryGetValue(p.Id, out var sold) ? sold : 0,
                Variants = p.Variants?.ToList() ?? new List<ProductVariant>()
            }).ToList();

            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> Index(int pageNumber = 1, int? categoryId = null, int? subCategoryId = null, decimal? minPrice = null, decimal? maxPrice = null, string query = "", string sortBy = "", string ram = "", string rom = "", string color = "", int? rating = null, string cpu = "")
        {
            const int pageSize = 16;
            if (pageNumber < 1) pageNumber = 1;

            IQueryable<Product> q = _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Variants)
                .Include(p => p.Specs) // Đảm bảo bảng thông số Specs được nạp đầy đủ
                .AsNoTracking();

            // --- Lọc cơ bản ---
            if (categoryId.HasValue && categoryId.Value != 0)
                q = q.Where(p => p.CategoryId == categoryId.Value);

            if (subCategoryId.HasValue && subCategoryId.Value != 0)
                q = q.Where(p => p.SubCategoryId == subCategoryId.Value);

            if (minPrice.HasValue)
                q = q.Where(p => p.DiscountedPrice >= minPrice.Value);

            if (maxPrice.HasValue)
                q = q.Where(p => p.DiscountedPrice <= maxPrice.Value);

            if (!string.IsNullOrWhiteSpace(query))
                q = q.Where(p => p.Name.Contains(query));

            // --- 🚀 CẬP NHẬT: LỌC NÂNG CAO CHO ĐỒ ÁN (SO SÁNH QUA BẢNG SPECS CỦA KIỆT) ---
            if (!string.IsNullOrWhiteSpace(cpu))
                q = q.Where(p => p.Specs != null && p.Specs.Cpu.Contains(cpu.Trim()));

            if (!string.IsNullOrWhiteSpace(ram))
                q = q.Where(p => p.Specs != null && p.Specs.Ram.Contains(ram.Trim()));

            if (!string.IsNullOrWhiteSpace(rom))
                q = q.Where(p => p.Specs != null && p.Specs.Storage.Contains(rom.Trim()));

            if (!string.IsNullOrWhiteSpace(color))
                q = q.Where(p => p.Variants.Any(v => v.Color.Contains(color.Trim())));

            if (rating.HasValue && rating.Value >= 1 && rating.Value <= 5)
                q = q.Where(p => p.Rating >= rating.Value);

            // --- Sắp xếp kết quả ---
            switch (sortBy)
            {
                case "giamgia":
                    q = q.OrderByDescending(p => p.DiscountPercent).ThenByDescending(p => p.Id);
                    break;
                case "moi":
                    q = q.OrderByDescending(p => p.Id);
                    break;
                case "giathapcao":
                    q = q.OrderBy(p => p.DiscountedPrice).ThenByDescending(p => p.Id);
                    break;
                case "giacaothap":
                    q = q.OrderByDescending(p => p.DiscountedPrice).ThenByDescending(p => p.Id);
                    break;
                case "banchay":
                    var soldQuery = _context.OrderDetails
                        .Where(od => od.Order.Status == OrderStatus.HoanTat)
                        .GroupBy(od => od.ProductId)
                        .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) });

                    q = q.GroupJoin(
                            soldQuery,
                            p => p.Id,
                            s => s.ProductId,
                            (p, s) => new { p, sold = s.Select(x => (int?)x.Sold).FirstOrDefault() ?? 0 }
                        )
                        .OrderByDescending(x => x.sold)
                        .ThenByDescending(x => x.p.Id)
                        .Select(x => x.p);
                    break;
                default:
                    q = q.OrderByDescending(p => p.IsHot).ThenByDescending(p => p.Id);
                    break;
            }

            // --- Xử lý phân trang mẫu ---
            var totalItems = await q.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (pageNumber > totalPages && totalPages > 0) pageNumber = totalPages;

            var pagedProducts = await q
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var productIds = pagedProducts.Select(p => p.Id).ToList();

            var soldDict = await _context.OrderDetails
                .Where(od => productIds.Contains(od.ProductId) && od.Order.Status == OrderStatus.HoanTat)
                .GroupBy(od => od.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Sold);

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
                SoldCount = soldDict.TryGetValue(p.Id, out var sold) ? sold : 0,
                Variants = p.Variants?.ToList() ?? new List<ProductVariant>()
            }).ToList();

            // --- Đồng bộ đóng gói dữ liệu đẩy ra View ---
            ViewBag.PageNumber = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.Categories = (await _categoryRepository.GetAllAsync()).Where(c => c.Id != 16).ToList();
            ViewBag.CategoryId = categoryId ?? 0;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Query = query;
            ViewBag.SubCategoryId = subCategoryId ?? 0;
            ViewBag.SortBy = sortBy;

            ViewBag.Ram = ram;
            ViewBag.Rom = rom;
            ViewBag.Color = color;
            ViewBag.Rating = rating;
            ViewBag.Cpu = cpu; // Đẩy thêm biến CPU ra View

            // Tạo danh sách cứng cho CPU Đồ án
            ViewBag.CpuList = await _context.ProductSpecs
    .AsNoTracking()
    .Where(s => !string.IsNullOrEmpty(s.Cpu))
    .Select(s => s.Cpu!.Trim())
    .Distinct()
    .ToListAsync();

            // 🌟 QUÉT ĐỘNG DANH SÁCH RAM VÀ ROM THỰC TẾ ĐANG CÓ TRONG DATABASE
            ViewBag.RamList = await _context.ProductSpecs
                .AsNoTracking()
                .Where(s => !string.IsNullOrEmpty(s.Ram))
                .Select(s => s.Ram!.Trim())
                .Distinct()
                .OrderBy(r => r) // Sắp xếp theo thứ tự tăng dần cho đẹp
                .ToListAsync();

            ViewBag.RomList = await _context.ProductSpecs
                .AsNoTracking()
                .Where(s => !string.IsNullOrEmpty(s.Storage))
                .Select(s => s.Storage!.Trim())
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            // 🌟 QUÉT ĐỘNG DANH SÁCH MÀU SẮC THỰC TẾ ĐANG CÓ TRONG BẢNG BIẾN THỂ (VARIANTS)
            ViewBag.ColorList = await _context.ProductVariants
                .AsNoTracking()
                .Where(v => !string.IsNullOrEmpty(v.Color))
                .Select(v => v.Color!.Trim())
                .Distinct()
                .OrderBy(c => c) // Sắp xếp theo bảng chữ cái A-Z cho ngăn nắp
                .ToListAsync();

            var subcats = (categoryId.HasValue && categoryId.Value != 0)
                ? await _context.SubCategories.Where(s => s.CategoryId == categoryId.Value).ToListAsync()
                : await _context.SubCategories.GroupBy(s => s.Name).Select(g => g.First()).ToListAsync();

            ViewBag.SubCategories = subcats;

            // --- Phần bốc danh sách hàng HOT (Giữ nguyên logic của Kiệt) ---
            var hotProductsData = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Variants)
                .Where(p => p.IsHot)
                .Take(4)
                .ToListAsync();

            var hotProductIds = hotProductsData.Select(p => p.Id).ToList();
            var hotSoldDict = await _context.OrderDetails
                .Where(od => hotProductIds.Contains(od.ProductId) && od.Order.Status == OrderStatus.HoanTat)
                .GroupBy(od => od.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(g => g.ProductId, g => g.Sold);

            ViewBag.HotProducts = hotProductsData.Select(p => new ProductWithSoldCount
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
                SoldCount = hotSoldDict.TryGetValue(p.Id, out var sold) ? sold : 0,
                Variants = p.Variants?.ToList() ?? new List<ProductVariant>()
            }).ToList();

            return View(model);
        }

        [Authorize]
        public async Task<IActionResult> Buy(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);
            if (cartItem != null)
                cartItem.Quantity += 1;
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

        public async Task<IActionResult> Display(int id, int? variantId, int? star, bool? hasImages, bool? verifiedOnly, string sort = "new", int page = 1)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Images)
                .Include(p => p.Specs)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();
            product.Images ??= new List<ProductImage>();

            int soldCount = await _context.OrderDetails
                .Where(od => od.ProductId == id && od.Order.Status == OrderStatus.HoanTat)
                .SumAsync(od => (int?)od.Quantity) ?? 0;

            var variants = await _context.ProductVariants
                .Where(v => v.ProductId == id)
                .OrderBy(v => v.Storage)
                .ThenBy(v => v.Color)
                .ToListAsync();

            ViewBag.Variants = variants;

            ProductVariant selectedVariant = null;
            if (variants.Any())
            {
                if (variantId.HasValue)
                    selectedVariant = variants.FirstOrDefault(v => v.Id == variantId.Value);

                selectedVariant ??= variants.FirstOrDefault(v => v.Stock > 0) ?? variants.First();
            }
            ViewBag.SelectedVariant = selectedVariant;

            const int pageSize = 5;
            if (page < 1) page = 1;

            var ratingStats = await _context.ProductRatings
                .Where(r => r.ProductId == id)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Avg = g.Average(x => (double)x.Stars),
                    Total = g.Count()
                })
                .FirstOrDefaultAsync();

            double avgStars = ratingStats?.Avg ?? 0.0;
            int totalRatings = ratingStats?.Total ?? 0;

            var countsByStar = await _context.ProductRatings
                .Where(r => r.ProductId == id)
                .GroupBy(r => r.Stars)
                .Select(g => new { Star = g.Key, Count = g.Count() })
                .ToListAsync();

            var starCounts = new List<int>();
            for (int s = 5; s >= 1; s--)
                starCounts.Add(countsByStar.FirstOrDefault(x => x.Star == s)?.Count ?? 0);

            ViewBag.AvgStars = avgStars;
            ViewBag.StarCounts = starCounts;
            ViewBag.TotalRatings = totalRatings;

            var verifiedUserIdsQuery = _context.OrderDetails
                .Where(od => od.ProductId == id && od.Order.Status == OrderStatus.HoanTat)
                .Select(od => od.Order.UserId)
                .Distinct();

            IQueryable<ProductRating> qRatings = _context.ProductRatings
                .Include(r => r.User)
                .Include(r => r.Images)
                .Include(r => r.Replies).ThenInclude(rep => rep.User)
                .Where(r => r.ProductId == id);

            if (star is >= 1 and <= 5)
                qRatings = qRatings.Where(r => r.Stars == star);

            if (hasImages == true)
                qRatings = qRatings.Where(r => r.Images.Any());

            if (verifiedOnly == true)
                qRatings = qRatings.Where(r => verifiedUserIdsQuery.Contains(r.UserId));

            qRatings = sort switch
            {
                "helpful" => qRatings.OrderByDescending(r => r.LikeCount - r.DislikeCount).ThenByDescending(r => r.CreatedAt),
                "high" => qRatings.OrderByDescending(r => r.Stars).ThenByDescending(r => r.CreatedAt),
                "low" => qRatings.OrderBy(r => r.Stars).ThenByDescending(r => r.CreatedAt),
                _ => qRatings.OrderByDescending(r => r.CreatedAt)
            };

            int totalFiltered = await qRatings.CountAsync();
            int totalPages = (int)Math.Ceiling(totalFiltered / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var ratings = await qRatings.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var verifiedSet = await verifiedUserIdsQuery.ToHashSetAsync();
            foreach (var r in ratings)
                r.IsVerifiedPurchase = verifiedSet.Contains(r.UserId);

            ViewBag.Ratings = ratings;

            ViewBag.RatingPage = page;
            ViewBag.RatingTotalPages = totalPages;
            ViewBag.FilterStar = star;
            ViewBag.FilterHasImages = hasImages == true;
            ViewBag.FilterVerifiedOnly = verifiedOnly == true;
            ViewBag.FilterSort = sort;

            ProductRating myRating = null;
            bool canReview = false;

            if (User.Identity.IsAuthenticated)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    myRating = await _context.ProductRatings
                        .Include(r => r.Images)
                        .FirstOrDefaultAsync(r => r.ProductId == id && r.UserId == user.Id);

                    canReview = await _context.Orders
                        .Include(o => o.OrderDetails)
                        .AnyAsync(o => o.UserId == user.Id
                            && o.OrderDetails.Any(od => od.ProductId == id)
                            && o.Status == OrderStatus.HoanTat);
                }
            }

            ViewBag.MyRating = myRating;
            ViewBag.CanReview = canReview;

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

            const string viewedCookieName = "RecentlyViewedProducts";
            List<int> viewedIds = new List<int>();

            if (Request.Cookies.TryGetValue(viewedCookieName, out var cookieValue) && !string.IsNullOrEmpty(cookieValue))
            {
                try { viewedIds = JsonSerializer.Deserialize<List<int>>(cookieValue) ?? new List<int>(); }
                catch { viewedIds = new List<int>(); }
            }

            viewedIds.Remove(id);
            viewedIds.Insert(0, id);
            if (viewedIds.Count > 10) viewedIds = viewedIds.Take(10).ToList();

            Response.Cookies.Append(
                viewedCookieName,
                JsonSerializer.Serialize(viewedIds),
                new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(7),
                    HttpOnly = false,
                    IsEssential = true
                }
            );

            var relatedEntities = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Variants)
                .Where(p => p.Id != id &&
                           (product.SubCategoryId.HasValue
                                ? p.SubCategoryId == product.SubCategoryId
                                : p.CategoryId == product.CategoryId))
                .OrderByDescending(p => p.IsHot)
                .Take(4)
                .ToListAsync();

            var popularEntities = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .Where(p => p.Id != id)
                .OrderByDescending(p => p.IsHot)
                .ThenByDescending(p => p.Rating)
                .Take(4)
                .ToListAsync();

            var orderIdsWithCurrentProduct = await _context.OrderDetails
                .Where(od => od.ProductId == id && od.Order.Status == OrderStatus.HoanTat)
                .Select(od => od.OrderId)
                .ToListAsync();

            var coBoughtProductIds = await _context.OrderDetails
                .Where(od => orderIdsWithCurrentProduct.Contains(od.OrderId) && od.ProductId != id)
                .GroupBy(od => od.ProductId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(4)
                .ToListAsync();

            var coBoughtEntities = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Variants)
                .Where(p => coBoughtProductIds.Contains(p.Id))
                .ToListAsync();

            if (!coBoughtEntities.Any())
            {
                coBoughtEntities = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .Where(p => p.Id != id && p.CategoryId == product.CategoryId)
                    .OrderByDescending(p => p.DiscountPercent)
                    .Take(4)
                    .ToListAsync();
            }

            var rawViewedEntities = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Variants)
                .Where(p => viewedIds.Contains(p.Id) && p.Id != id)
                .ToListAsync();

            var viewedEntities = viewedIds
                .Select(vId => rawViewedEntities.FirstOrDefault(p => p.Id == vId))
                .Where(p => p != null)
                .ToList();

            var allIds = relatedEntities.Select(p => p.Id)
                .Concat(viewedEntities.Select(p => p.Id))
                .Concat(popularEntities.Select(p => p.Id))
                .Concat(coBoughtEntities.Select(p => p.Id))
                .Distinct().ToList();

            var soldDict = allIds.Any()
                ? await _context.OrderDetails
                    .Where(od => allIds.Contains(od.ProductId) && od.Order.Status == OrderStatus.HoanTat)
                    .GroupBy(od => od.ProductId)
                    .Select(g => new { Id = g.Key, Sold = g.Sum(x => x.Quantity) })
                    .ToDictionaryAsync(g => g.Id, g => g.Sold)
                : new Dictionary<int, int>();

            ProductWithSoldCount MapToVm(Product p) => new ProductWithSoldCount
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                Rating = p.Rating,
                DiscountedPrice = p.DiscountedPrice,
                DiscountPercent = p.DiscountPercent,
                SoldCount = soldDict.TryGetValue(p.Id, out var sold) ? sold : 0,
                Variants = p.Variants?.ToList() ?? new List<ProductVariant>()
            };

            ViewBag.RelatedProducts = relatedEntities.Select(MapToVm).ToList();
            ViewBag.ViewedProducts = viewedEntities.Select(MapToVm).ToList();
            ViewBag.PopularProducts = popularEntities.Select(MapToVm).ToList();
            ViewBag.CoBoughtProducts = coBoughtEntities.Select(MapToVm).ToList();

            var faqs = await _context.ProductFaqs
                .AsNoTracking()
                .Where(f => f.IsActive && (f.ProductId == null || f.ProductId == id))
                .OrderBy(f => f.SortOrder)
                .ThenByDescending(f => f.Id)
                .Take(20)
                .ToListAsync();

            ViewBag.Faqs = faqs;

            bool isStaff = User.IsInRole("Admin") || User.IsInRole("Employer");
            string? currentUserId = null;
            if (User.Identity.IsAuthenticated)
            {
                var u = await _userManager.GetUserAsync(User);
                currentUserId = u?.Id;
            }

            ViewBag.CurrentUserId = currentUserId;

            IQueryable<ProductQuestion> qQas = _context.ProductQuestions
                .AsNoTracking()
                .Include(q => q.User)
                .Include(q => q.Replies).ThenInclude(r => r.User)
                .Where(q => q.ProductId == id);

            if (!isStaff)
            {
                if (string.IsNullOrEmpty(currentUserId))
                    qQas = qQas.Where(q => q.IsApproved);
                else
                    qQas = qQas.Where(q => q.IsApproved || q.UserId == currentUserId);
            }

            var qas = await qQas.OrderByDescending(q => q.CreatedAt).Take(20).ToListAsync();
            ViewBag.QAs = qas;

            return View(displayModel);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return NotFound();
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
        public async Task<IActionResult> SubmitRating(int productId, int stars, string comment, List<IFormFile>? images)
        {
            if (stars < 1 || stars > 5) return BadRequest("Điểm phải từ 1 đến 5 sao.");
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (user.IsBanned)
            {
                TempData["ErrorMessage"] = "Tài khoản của bạn đã bị khóa chức năng viết bình luận/đánh giá do vi phạm tiêu chuẩn!";
                return RedirectToAction("Display", new { id = productId });
            }

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

            var existing = await _context.ProductRatings
                .Include(r => r.Images)
                .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == user.Id);

            string checkContent = (comment ?? "").ToLower();
            string[] bannedWords = { "lừa đảo", "vcl", "dm", "đm", "hàng giả", "fake", "bú" };
            bool containsBadWords = bannedWords.Any(word => checkContent.Contains(word));
            bool approveStatus = !containsBadWords;

            if (existing == null)
            {
                existing = new ProductRating
                {
                    ProductId = productId,
                    UserId = user.Id,
                    Stars = stars,
                    Comment = comment,
                    CreatedAt = DateTime.Now,
                    IsApproved = approveStatus
                };
                _context.ProductRatings.Add(existing);
            }
            else
            {
                existing.Stars = stars;
                existing.Comment = comment;
                existing.UpdatedAt = DateTime.Now;
                existing.IsApproved = approveStatus;
                _context.ProductRatings.Update(existing);
            }
            await _context.SaveChangesAsync();

            if (images?.Any() == true)
            {
                var allowExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var folder = Path.Combine("wwwroot", "uploads", "ratings");
                Directory.CreateDirectory(folder);

                foreach (var file in images.Take(5))
                {
                    if (file.Length == 0 || file.Length > 2_000_000) continue;

                    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!allowExt.Contains(ext)) continue;

                    var fileName = $"{Guid.NewGuid():N}{ext}";
                    var savePath = Path.Combine(folder, fileName);

                    using var stream = System.IO.File.Create(savePath);
                    await file.CopyToAsync(stream);

                    _context.ProductRatingImages.Add(new ProductRatingImage
                    {
                        ProductRatingId = existing.Id,
                        Url = $"/uploads/ratings/{fileName}"
                    });
                }
                await _context.SaveChangesAsync();
            }

            var avgStars = (await _context.ProductRatings
                .Where(r => r.ProductId == productId && r.IsApproved)
                .Select(r => (double?)r.Stars)
                .AverageAsync()) ?? 0.0;

            var dbProduct = await _context.Products.FindAsync(productId);
            if (dbProduct != null)
            {
                dbProduct.Rating = avgStars;
                await _context.SaveChangesAsync();
            }

            if (containsBadWords)
                TempData["SuccessMessage"] = "Đánh giá đã được gửi đi và đang nằm trong danh sách kiểm duyệt của Admin.";
            else
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

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoteRating(int ratingId, bool isLike)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var rating = await _context.ProductRatings.FirstOrDefaultAsync(r => r.Id == ratingId);
            if (rating == null) return NotFound();

            if (rating.UserId == user.Id)
                return BadRequest("Không thể vote đánh giá của chính bạn.");

            var vote = await _context.ProductRatingVotes
                .SingleOrDefaultAsync(v => v.ProductRatingId == ratingId && v.UserId == user.Id);

            if (vote == null)
            {
                _context.ProductRatingVotes.Add(new ProductRatingVote
                {
                    ProductRatingId = ratingId,
                    UserId = user.Id,
                    IsLike = isLike
                });

                if (isLike) rating.LikeCount++;
                else rating.DislikeCount++;
            }
            else
            {
                if (vote.IsLike == isLike)
                {
                    _context.ProductRatingVotes.Remove(vote);
                    if (isLike) rating.LikeCount--;
                    else rating.DislikeCount--;
                }
                else
                {
                    vote.IsLike = isLike;
                    if (isLike) { rating.LikeCount++; rating.DislikeCount--; }
                    else { rating.DislikeCount++; rating.LikeCount--; }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { like = rating.LikeCount, dislike = rating.DislikeCount });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportRating(int ratingId, string reason)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 200)
                return BadRequest("Lý do không hợp lệ.");

            var existed = await _context.ProductRatingReports
                .AnyAsync(x => x.ProductRatingId == ratingId && x.UserId == user.Id);

            if (existed)
                return Json(new { ok = false, message = "Bạn đã báo cáo đánh giá này rồi." });

            _context.ProductRatingReports.Add(new ProductRatingReport
            {
                ProductRatingId = ratingId,
                UserId = user.Id,
                Reason = reason.Trim()
            });

            await _context.SaveChangesAsync();
            return Json(new { ok = true });
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AskQuestion(int productId, string question)
        {
            if (User.IsInRole("Admin") || User.IsInRole("Employer"))
            {
                TempData["ErrorMessage"] = "Tài khoản nhân viên không thể đặt câu hỏi như khách hàng.";
                return RedirectToAction("Display", new { id = productId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (user.IsBanned)
            {
                TempData["ErrorMessage"] = "Tài khoản của bạn đã bị khóa chức năng đặt câu hỏi thảo luận!";
                return RedirectToAction("Display", new { id = productId });
            }

            question = (question ?? "").Trim();

            if (string.IsNullOrWhiteSpace(question))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập câu hỏi.";
                return RedirectToAction("Display", new { id = productId });
            }

            if (question.Length > 800)
            {
                TempData["ErrorMessage"] = "Câu hỏi quá dài (tối đa 800 ký tự).";
                return RedirectToAction("Display", new { id = productId });
            }

            string checkContent = question.ToLower();
            string[] bannedWords = { "lừa đảo", "vcl", "dm", "đm", "hàng giả", "fake", "bú" };
            bool containsBadWords = bannedWords.Any(word => checkContent.Contains(word));

            var qa = new ProductQuestion
            {
                ProductId = productId,
                UserId = user.Id,
                Question = question,
                CreatedAt = DateTime.Now,
                IsApproved = !containsBadWords
            };

            _context.ProductQuestions.Add(qa);
            await _context.SaveChangesAsync();

            if (containsBadWords)
                TempData["SuccessMessage"] = "Câu hỏi của bạn chứa từ khóa cần xác minh và đã được chuyển đến bộ phận kiểm duyệt.";
            else
                TempData["SuccessMessage"] = "Đã gửi câu hỏi! Câu hỏi sẽ hiển thị ngay lập tức.";

            return RedirectToAction("Display", new { id = productId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Employer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReplyQuestion(int questionId, string content)
        {
            content = (content ?? "").Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["ErrorMessage"] = "Nội dung trả lời không được để trống.";
                var pid = (await _context.ProductQuestions.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == questionId))?.ProductId ?? 0;
                return RedirectToAction("Display", new { id = pid });
            }

            if (content.Length > 1000)
            {
                TempData["ErrorMessage"] = "Nội dung trả lời quá dài (tối đa 1000 ký tự).";
                var pid = (await _context.ProductQuestions.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == questionId))?.ProductId ?? 0;
                return RedirectToAction("Display", new { id = pid });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var question = await _context.ProductQuestions.FirstOrDefaultAsync(x => x.Id == questionId);
            if (question == null) return NotFound();

            question.IsApproved = true;

            _context.ProductQuestionReplies.Add(new ProductQuestionReply
            {
                ProductQuestionId = questionId,
                Content = content,
                UserId = user.Id,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã phản hồi Q&A!";
            return RedirectToAction("Display", new { id = question.ProductId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Employer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditQuestionReply(int replyId, string content)
        {
            content = (content ?? "").Trim();

            if (string.IsNullOrWhiteSpace(content))
                return BadRequest("Nội dung không được để trống.");

            if (content.Length > 1000)
                return BadRequest("Nội dung quá dài (tối đa 1000 ký tự).");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var reply = await _context.ProductQuestionReplies
                .Include(r => r.ProductQuestion)
                .FirstOrDefaultAsync(r => r.Id == replyId);

            if (reply == null) return NotFound();

            if (!User.IsInRole("Admin") && reply.UserId != user.Id)
                return Forbid();

            reply.Content = content;
            reply.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã cập nhật câu trả lời.";
            return RedirectToAction("Display", new { id = reply.ProductQuestion.ProductId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Employer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestionReply(int replyId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var reply = await _context.ProductQuestionReplies
                .Include(r => r.ProductQuestion)
                .FirstOrDefaultAsync(r => r.Id == replyId);

            if (reply == null) return NotFound();

            if (!User.IsInRole("Admin") && reply.UserId != user.Id)
                return Forbid();

            _context.ProductQuestionReplies.Remove(reply);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xoá câu trả lời.";
            return RedirectToAction("Display", new { id = reply.ProductQuestion.ProductId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Employer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveQuestion(int questionId)
        {
            var q = await _context.ProductQuestions.FirstOrDefaultAsync(x => x.Id == questionId);
            if (q == null) return NotFound();

            q.IsApproved = true;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã duyệt câu hỏi.";
            return RedirectToAction("Display", new { id = q.ProductId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Employer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HideQuestion(int questionId)
        {
            var q = await _context.ProductQuestions.FirstOrDefaultAsync(x => x.Id == questionId);
            if (q == null) return NotFound();

            q.IsApproved = false;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã ẩn câu hỏi.";
            return RedirectToAction("Display", new { id = q.ProductId });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int questionId)
        {
            var q = await _context.ProductQuestions
                .Include(x => x.Replies)
                .FirstOrDefaultAsync(x => x.Id == questionId);

            if (q == null) return NotFound();

            if (q.Replies != null && q.Replies.Any())
                _context.ProductQuestionReplies.RemoveRange(q.Replies);

            _context.ProductQuestions.Remove(q);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xoá câu hỏi.";
            return RedirectToAction("Display", new { id = q.ProductId });
        }

        // =========================================================================
        // 🚀 CẬP NHẬT 1: ACTION NHẬN FORM ĐĂNG KÝ THEO DÕI GIÁ MONG MUỐN (WISHLIST TRACK)
        // =========================================================================
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreatePriceAlert(int productId, decimal targetPrice)
        {
            if (targetPrice <= 0)
                return Json(new { success = false, message = "Vui lòng nhập mức giá kỳ vọng hợp lệ!" });

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Bạn cần đăng nhập hệ thống!" });

            // Kiểm tra khách đã đăng ký nhận thông báo cho sản phẩm này chưa
            var existingAlert = await _context.PriceAlerts
                .FirstOrDefaultAsync(a => a.ProductId == productId && a.UserId == user.Id && !a.IsTriggered);

            if (existingAlert != null)
            {
                existingAlert.TargetPrice = targetPrice; // Cập nhật lại giá kỳ vọng mới
                _context.PriceAlerts.Update(existingAlert);
            }
            else
            {
                var newAlert = new PriceAlert
                {
                    ProductId = productId,
                    UserId = user.Id,
                    Email = user.Email ?? "",
                    TargetPrice = targetPrice,
                    IsTriggered = false,
                    CreatedDate = DateTime.Now
                };
                _context.PriceAlerts.Add(newAlert);
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Đã bật theo dõi! Hệ thống sẽ thông báo tới Email ({user.Email}) khi giá giảm xuống mốc {targetPrice:N0} đ." });
        }

        // =========================================================================
        // 🚀 CẬP NHẬT 2: ACTION TRẢ DỮ LIỆU JSON ĐỂ VẼ BIỂU ĐỒ LỊCH SỬ BIẾN ĐỘNG GIÁ
        // =========================================================================
        [HttpGet]
        public async Task<IActionResult> GetPriceHistory(int productId)
        {
            var historyData = await _context.ProductPriceHistories
                .Where(h => h.ProductId == productId)
                .OrderBy(h => h.ChangeDate)
                .ToListAsync();

            // Nếu sản phẩm chưa từng có log đổi giá, gộp giá hiện tại của sản phẩm làm mốc gốc
            if (!historyData.Any())
            {
                var product = await _context.Products.FindAsync(productId);
                if (product != null)
                {
                    return Json(new
                    {
                        dates = new[] { DateTime.Now.ToString("dd/MM") },
                        prices = new[] { product.DiscountedPrice }
                    });
                }
            }

            return Json(new
            {
                dates = historyData.Select(h => h.ChangeDate.ToString("dd/MM/yyyy")),
                prices = historyData.Select(h => h.Price)
            });
        }
    }
}