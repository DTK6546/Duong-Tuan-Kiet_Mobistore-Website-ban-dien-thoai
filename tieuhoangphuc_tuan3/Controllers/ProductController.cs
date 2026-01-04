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
            const int pageSize = 16;
            if (pageNumber < 1) pageNumber = 1;

            // Base query
            IQueryable<Product> q = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Variants);

            // Filters
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

            // Sort
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
                    // Bán chạy: join sold count 1 lần (không N+1)
                    var soldQuery = _context.OrderDetails
                        .Where(od => od.Order.Status == OrderStatus.HoanTat)
                        .GroupBy(od => od.ProductId)
                        .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) });

                    q = q
                        .GroupJoin(
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
                    q = q.OrderByDescending(p => p.Id);
                    break;
            }

            // Paging
            var totalItems = await q.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            var pagedProducts = await q
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var productIds = pagedProducts.Select(p => p.Id).ToList();

            // SoldCount cho danh sách đang hiển thị
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

            // ViewBag giữ nguyên như bạn đang dùng
            ViewBag.PageNumber = pageNumber;
            ViewBag.TotalPages = totalPages;
            ViewBag.Categories = (await _categoryRepository.GetAllAsync()).Where(c => c.Id != 16).ToList();
            ViewBag.CategoryId = categoryId ?? 0;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.Query = query;
            ViewBag.SubCategoryId = subCategoryId ?? 0;
            ViewBag.SortBy = sortBy;

            var subcats = (categoryId.HasValue && categoryId.Value != 0)
                ? await _context.SubCategories.Where(s => s.CategoryId == categoryId.Value).ToListAsync()
                : await _context.SubCategories
                    .GroupBy(s => s.Name)
                    .Select(g => g.First())
                    .ToListAsync();

            ViewBag.SubCategories = subcats;

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

        public async Task<IActionResult> Display(int id, int? variantId, int? star, bool? hasImages, bool? verifiedOnly, string sort = "new", int page = 1)
        {
            // 1) Product + related tables
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Images)
                .Include(p => p.Specs)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (product == null) return NotFound();
            product.Images ??= new List<ProductImage>();

            // 2) Sold count
            int soldCount = await _context.OrderDetails
                .Where(od => od.ProductId == id && od.Order.Status == OrderStatus.HoanTat)
                .SumAsync(od => (int?)od.Quantity) ?? 0;

            // 3) Variants
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

            // =========================
            // 4) REVIEW: stats + filter/sort + paging + verified badge (NO N+1)
            // =========================
            const int pageSize = 5;
            if (page < 1) page = 1;

            // Stats (tất cả review)
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

            // Verified userIds (đã mua + hoàn tất)
            var verifiedUserIdsQuery = _context.OrderDetails
                .Where(od => od.ProductId == id && od.Order.Status == OrderStatus.HoanTat)
                .Select(od => od.Order.UserId)
                .Distinct();

            // Query list
            IQueryable<ProductRating> qRatings = _context.ProductRatings
                .Include(r => r.User)
                .Include(r => r.Images) // ✅ ảnh review
                .Include(r => r.Replies).ThenInclude(rep => rep.User)
                .Where(r => r.ProductId == id);

            // Filters
            if (star is >= 1 and <= 5)
                qRatings = qRatings.Where(r => r.Stars == star);

            if (hasImages == true)
                qRatings = qRatings.Where(r => r.Images.Any());

            if (verifiedOnly == true)
                qRatings = qRatings.Where(r => verifiedUserIdsQuery.Contains(r.UserId));

            // Sort
            qRatings = sort switch
            {
                "helpful" => qRatings
                    .OrderByDescending(r => r.LikeCount - r.DislikeCount)
                    .ThenByDescending(r => r.CreatedAt),

                "high" => qRatings
                    .OrderByDescending(r => r.Stars)
                    .ThenByDescending(r => r.CreatedAt),

                "low" => qRatings
                    .OrderBy(r => r.Stars)
                    .ThenByDescending(r => r.CreatedAt),

                _ => qRatings.OrderByDescending(r => r.CreatedAt)
            };

            // Paging
            int totalFiltered = await qRatings.CountAsync();
            int totalPages = (int)Math.Ceiling(totalFiltered / (double)pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var ratings = await qRatings
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Set badge verified (1 lần)
            var verifiedSet = await verifiedUserIdsQuery.ToHashSetAsync();
            foreach (var r in ratings)
                r.IsVerifiedPurchase = verifiedSet.Contains(r.UserId);

            ViewBag.Ratings = ratings;

            // Keep filter in ViewBag
            ViewBag.RatingPage = page;
            ViewBag.RatingTotalPages = totalPages;
            ViewBag.FilterStar = star;
            ViewBag.FilterHasImages = hasImages == true;
            ViewBag.FilterVerifiedOnly = verifiedOnly == true;
            ViewBag.FilterSort = sort;

            // MyRating + CanReview (đã mua mới được đánh giá)
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

            // 5) Main ViewModel
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

            // 6) Related + Viewed (giữ logic cookie của bạn)
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
                .Where(p => p.Id != id && p.SubCategoryId == product.SubCategoryId)
                .OrderByDescending(p => p.Id)
                .Take(8)
                .ToListAsync();

            var viewedEntities = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Variants)
                .Where(p => viewedIds.Contains(p.Id) && p.Id != id)
                .ToListAsync();

            // mapping sold count
            var allIds = relatedEntities.Select(p => p.Id).Concat(viewedEntities.Select(p => p.Id)).Distinct().ToList();
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
                SoldCount = soldDict.ContainsKey(p.Id) ? soldDict[p.Id] : 0,
                Variants = p.Variants?.ToList() ?? new List<ProductVariant>()
            };

            ViewBag.RelatedProducts = relatedEntities.Select(MapToVm).ToList();
            ViewBag.ViewedProducts = viewedEntities.Select(MapToVm).ToList();

            // =========================
            // 7) FAQ + Q&A
            // =========================

            // FAQ: lấy FAQ chung + FAQ theo sản phẩm
            var faqs = await _context.ProductFaqs
                .AsNoTracking()
                .Where(f => f.IsActive && (f.ProductId == null || f.ProductId == id))
                .OrderBy(f => f.SortOrder)
                .ThenByDescending(f => f.Id)
                .Take(20)
                .ToListAsync();

            ViewBag.Faqs = faqs;

            // Q&A: Web thật
            // - Admin/Employer: thấy tất cả
            // - User thường: thấy câu đã duyệt + câu của chính mình (dù chưa duyệt)
            // - Chưa đăng nhập: chỉ thấy câu đã duyệt
            bool isStaff = User.IsInRole("Admin") || User.IsInRole("Employer");

            string? currentUserId = null;
            if (User.Identity.IsAuthenticated)
            {
                var u = await _userManager.GetUserAsync(User);
                currentUserId = u?.Id;
            }

            IQueryable<ProductQuestion> qQas = _context.ProductQuestions
                .AsNoTracking()
                .Include(q => q.User)
                .Include(q => q.Replies).ThenInclude(r => r.User)
                .Where(q => q.ProductId == id);

            if (!isStaff)
            {
                if (string.IsNullOrEmpty(currentUserId))
                {
                    // khách chưa login: chỉ thấy câu đã duyệt
                    qQas = qQas.Where(q => q.IsApproved);
                }
                else
                {
                    // khách login: thấy đã duyệt + câu của mình
                    qQas = qQas.Where(q => q.IsApproved || q.UserId == currentUserId);
                }
            }

            var qas = await qQas
                .OrderByDescending(q => q.CreatedAt)
                .Take(20)
                .ToListAsync();

            ViewBag.QAs = qas;

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
        public async Task<IActionResult> SubmitRating(int productId, int stars, string comment, List<IFormFile>? images)
        {
            if (stars < 1 || stars > 5)
                return BadRequest("Điểm phải từ 1 đến 5 sao.");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

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

            if (existing == null)
            {
                existing = new ProductRating
                {
                    ProductId = productId,
                    UserId = user.Id,
                    Stars = stars,
                    Comment = comment,
                    CreatedAt = DateTime.Now
                };
                _context.ProductRatings.Add(existing);
                await _context.SaveChangesAsync(); // cần Id để lưu ảnh
            }
            else
            {
                existing.Stars = stars;
                existing.Comment = comment;
                existing.UpdatedAt = DateTime.Now;
                _context.ProductRatings.Update(existing);
                await _context.SaveChangesAsync();
            }

            // ✅ upload ảnh (tối đa 5 ảnh, mỗi ảnh <= 2MB)
            if (images?.Any() == true)
            {
                var allowExt = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var folder = Path.Combine("wwwroot", "uploads", "ratings");
                Directory.CreateDirectory(folder);

                // (tuỳ chọn) nếu muốn thay ảnh cũ khi update:
                // _context.ProductRatingImages.RemoveRange(existing.Images);
                // await _context.SaveChangesAsync();

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

            // update product avg rating
            var avgStars = await _context.ProductRatings
                .Where(r => r.ProductId == productId)
                .AverageAsync(r => (double)r.Stars);

            var product = await _context.Products.FindAsync(productId);
            if (product != null)
            {
                product.Rating = avgStars;
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

        // =====================
        // Q&A - Ask / Reply / Edit / Delete
        // =====================

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AskQuestion(int productId, string question)
        {
            // ✅ Web thật: Staff không đặt câu hỏi như khách
            if (User.IsInRole("Admin") || User.IsInRole("Employer"))
            {
                TempData["ErrorMessage"] = "Tài khoản nhân viên không thể đặt câu hỏi như khách hàng.";
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

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            // ✅ Web thật: câu hỏi chờ duyệt
            var qa = new ProductQuestion
            {
                ProductId = productId,
                UserId = user.Id,
                Question = question,
                CreatedAt = DateTime.Now,
                IsApproved = false
            };

            _context.ProductQuestions.Add(qa);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã gửi câu hỏi! Câu hỏi sẽ hiển thị sau khi được duyệt.";
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

            // ✅ Staff trả lời -> auto approve
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

            // ✅ Web thật: Employer chỉ sửa reply của chính mình, Admin sửa tất cả
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

            // ✅ Web thật: Employer chỉ xoá reply của chính mình, Admin xoá tất cả
            if (!User.IsInRole("Admin") && reply.UserId != user.Id)
                return Forbid();

            _context.ProductQuestionReplies.Remove(reply);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xoá câu trả lời.";
            return RedirectToAction("Display", new { id = reply.ProductQuestion.ProductId });
        }

        // =====================
        // Q&A Moderation (Web thật): Approve / Hide / Delete Question
        // =====================

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

        // (Tuỳ chọn) Web thật thường có xoá câu hỏi (admin làm được)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteQuestion(int questionId)
        {
            var q = await _context.ProductQuestions
                .Include(x => x.Replies)
                .FirstOrDefaultAsync(x => x.Id == questionId);

            if (q == null) return NotFound();

            // xoá replies trước (nếu bạn chưa cấu hình cascade)
            if (q.Replies != null && q.Replies.Any())
                _context.ProductQuestionReplies.RemoveRange(q.Replies);

            _context.ProductQuestions.Remove(q);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đã xoá câu hỏi.";
            return RedirectToAction("Display", new { id = q.ProductId });
        }

    }
}
