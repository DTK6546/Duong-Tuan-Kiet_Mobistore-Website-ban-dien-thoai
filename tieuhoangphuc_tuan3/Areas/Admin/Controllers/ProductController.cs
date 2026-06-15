using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;
using WebBanDienThoai.Repositories;
using WebBanDienThoai.Services.SignalR;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _chatHub;

        public ProductController(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            ApplicationDbContext context,
            IHubContext<ChatHub> chatHub)
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _context = context;
            _chatHub = chatHub;
        }

        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        public async Task<IActionResult> Index(string searchTerm, string sortOrder)
        {
            var products = await _productRepository.GetAllAsync();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                products = products.Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                               p.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            switch (sortOrder)
            {
                case "price_asc":
                    products = products.OrderBy(p => p.Price).ToList();
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price).ToList();
                    break;
            }

            var lowStockProducts = products.Where(p => p.Quantity <= p.MinStockLevel).ToList();
            ViewBag.LowStockProducts = lowStockProducts;

            ViewBag.CurrentSearch = searchTerm;
            ViewBag.CurrentSort = sortOrder;

            return View(products);
        }

        [Authorize(Roles = SD.Role_Admin)]
        [HttpGet]
        public async Task<IActionResult> Add()
        {
            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name");

            var subCategories = _context.SubCategories.ToList();
            ViewBag.SubCategories = new SelectList(subCategories, "Id", "Name");

            return View();
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> Add(Product product, IFormFile imageUrl, IFormFile[] additionalImages)
        {
            if (ModelState.IsValid)
            {
                if (imageUrl != null)
                {
                    product.ImageUrl = await SaveImage(imageUrl);
                }

                if (product.Price > 0 && product.DiscountPercent > 0)
                    product.DiscountedPrice = product.Price - (product.Price * product.DiscountPercent / 100);
                else
                    product.DiscountedPrice = product.Price;

                product.LastImportDate = DateTime.Now;

                await _productRepository.AddAsync(product);

                // ✨ ĐỒNG BỘ: Khởi tạo mốc lịch sử giá đầu tiên khi tạo mới sản phẩm
                var initialHistory = new ProductPriceHistory
                {
                    ProductId = product.Id,
                    Price = product.DiscountedPrice,
                    ChangeDate = DateTime.Now
                };
                _context.ProductPriceHistories.Add(initialHistory);
                await _context.SaveChangesAsync();

                if (additionalImages != null && additionalImages.Length > 0)
                {
                    foreach (var file in additionalImages)
                    {
                        if (file != null && file.Length > 0)
                        {
                            var imageUrlSaved = await SaveImage(file);

                            var img = new ProductImage
                            {
                                Url = imageUrlSaved,
                                ProductId = product.Id
                            };

                            _context.ProductImages.Add(img);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                var productUrl = Url.Action("Display", "Product", new { area = "", id = product.Id }, Request.Scheme);
                var summary = $"Giá: {product.DiscountedPrice:N0}₫" +
                              (product.DiscountPercent > 0 ? $" (Giảm {product.DiscountPercent}%)" : "");

                await _chatHub.Clients.All.SendAsync(
                    "ReceiveProductNotification",
                    $"Sản phẩm mới: {product.Name}",
                    summary,
                    productUrl,
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                );

                TempData["SuccessMessage"] = "Sản phẩm đã được thêm thành công!";
                return RedirectToAction(nameof(Index));
            }

            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);

            var subCategories = _context.SubCategories
                .Where(s => s.CategoryId == product.CategoryId)
                .ToList();
            ViewBag.SubCategories = new SelectList(subCategories, "Id", "Name", product.SubCategoryId);

            return View(product);
        }

        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> Display(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        public async Task<IActionResult> Update(int id)
        {
            var product = await _context.Products
                .Include(p => p.Images)
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return NotFound();

            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);

            var subCategories = _context.SubCategories
                .Where(s => s.CategoryId == product.CategoryId)
                .ToList();
            ViewBag.SubCategories = new SelectList(subCategories, "Id", "Name", product.SubCategoryId);

            return View(product);
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> Update(int id, Product product, IFormFile imageUrl, IFormFile[] additionalImages, int[] deleteImageIds)
        {
            ModelState.Remove("ImageUrl");
            if (id != product.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var existingProduct = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
                if (existingProduct == null)
                    return NotFound();

                if (imageUrl != null)
                    product.ImageUrl = await SaveImage(imageUrl);
                else
                    product.ImageUrl = existingProduct.ImageUrl;

                // Tính toán giá mới từ dữ liệu Form gửi lên
                decimal newDiscountedPrice = product.Price;
                if (product.Price > 0 && product.DiscountPercent > 0)
                    newDiscountedPrice = product.Price - (product.Price * product.DiscountPercent / 100);
                else
                    newDiscountedPrice = product.Price;

                // =========================================================================
                // 📉 KIỂM TRA BIẾN ĐỘNG GIÁ & GHI LOG LỊCH SỬ GIÁ
                // =========================================================================
                bool isPriceDropped = newDiscountedPrice < existingProduct.DiscountedPrice; // Cờ kiểm tra thực sự hạ giá

                if (existingProduct.DiscountedPrice != newDiscountedPrice)
                {
                    var priceHistoryLog = new ProductPriceHistory
                    {
                        ProductId = existingProduct.Id,
                        Price = newDiscountedPrice,
                        ChangeDate = DateTime.Now
                    };
                    _context.ProductPriceHistories.Add(priceHistoryLog);
                }

                existingProduct.Name = product.Name;
                existingProduct.Price = product.Price;
                existingProduct.Description = product.Description;
                existingProduct.DiscountPercent = product.DiscountPercent;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.SubCategoryId = product.SubCategoryId;
                existingProduct.ImageUrl = product.ImageUrl;
                existingProduct.DiscountedPrice = newDiscountedPrice;
                existingProduct.ServiceCommitment = product.ServiceCommitment;
                existingProduct.IsHot = product.IsHot;

                if (product.Quantity > existingProduct.Quantity)
                    existingProduct.LastImportDate = DateTime.Now;

                existingProduct.Quantity = product.Quantity;
                existingProduct.MinStockLevel = product.MinStockLevel;

                if (deleteImageIds != null && deleteImageIds.Length > 0)
                {
                    foreach (var imgId in deleteImageIds)
                    {
                        var img = existingProduct.Images.FirstOrDefault(i => i.Id == imgId);
                        if (img != null)
                        {
                            _context.ProductImages.Remove(img);
                            var imagePath = Path.Combine("wwwroot/images", Path.GetFileName(img.Url));
                            if (System.IO.File.Exists(imagePath))
                                System.IO.File.Delete(imagePath);
                        }
                    }
                }

                if (additionalImages != null && additionalImages.Length > 0)
                {
                    foreach (var file in additionalImages)
                    {
                        if (file != null && file.Length > 0)
                        {
                            var imageUrlSaved = await SaveImage(file);
                            var newImg = new ProductImage
                            {
                                Url = imageUrlSaved,
                                ProductId = existingProduct.Id
                            };
                            _context.ProductImages.Add(newImg);
                        }
                    }
                }

                // 💾 BƯỚC 1: Lưu thay đổi của sản phẩm chính và log lịch sử giá xuống Database trước
                _context.Products.Update(existingProduct);
                await _context.SaveChangesAsync();

                // 🔔 BƯỚC 2: Nếu giá thực tế được hạ xuống, kích hoạt bẫy quét Alert săn deal ngay sau đó
                if (isPriceDropped)
                {
                    await TriggerPriceDropAlertsAsync(existingProduct.Id, newDiscountedPrice, existingProduct.Name);
                }

                var url = Url.Action("Display", "Product", new { area = "", id = existingProduct.Id }, Request.Scheme);
                var summary = $"Giá: {existingProduct.DiscountedPrice:N0}₫" +
                              (existingProduct.DiscountPercent > 0 ? $" (Giảm {existingProduct.DiscountPercent}%)" : "");

                await _chatHub.Clients.All.SendAsync(
                    "ReceiveProductNotification",
                    $"Cập nhật: {existingProduct.Name}",
                    summary,
                    url,
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm")
                );

                TempData["SuccessMessage"] = "Sản phẩm đã được cập nhật thành công!";
                return RedirectToAction(nameof(Index));
            }

            var categories = await _categoryRepository.GetAllAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", product.CategoryId);

            var subCategories = _context.SubCategories
                .Where(s => s.CategoryId == product.CategoryId)
                .ToList();
            ViewBag.SubCategories = new SelectList(subCategories, "Id", "Name", product.SubCategoryId);

            return View(product);
        }

        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return NotFound();

            var name = product.Name;

            if (product.Images != null && product.Images.Any())
            {
                foreach (var image in product.Images)
                {
                    _context.ProductImages.Remove(image);
                    var imagePath = Path.Combine("wwwroot/images", Path.GetFileName(image.Url));
                    if (System.IO.File.Exists(imagePath))
                        System.IO.File.Delete(imagePath);
                }
                await _context.SaveChangesAsync();
            }

            // Xóa lịch sử giá và alert liên quan trước khi xóa sản phẩm chính để tránh lỗi Khóa ngoại
            var relatedHistory = await _context.ProductPriceHistories.Where(h => h.ProductId == id).ToListAsync();
            _context.ProductPriceHistories.RemoveRange(relatedHistory);
            var relatedAlerts = await _context.PriceAlerts.Where(a => a.ProductId == id).ToListAsync();
            _context.PriceAlerts.RemoveRange(relatedAlerts);

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            await _chatHub.Clients.All.SendAsync(
                "ReceiveProductNotification",
                $"Đã xóa: {name}",
                "Sản phẩm đã bị xóa khỏi hệ thống.",
                null,
                DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            );

            TempData["SuccessMessage"] = "Sản phẩm và dữ liệu liên kết đã được xóa thành công!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        public async Task<IActionResult> ToggleHot(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm." });
            }

            product.IsHot = !product.IsHot;
            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            return Json(new { success = true, isHot = product.IsHot, message = "Đã cập nhật trạng thái nổi bật!" });
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

        [HttpGet]
        public JsonResult GetSubCategories(int categoryId)
        {
            var subcats = _context.SubCategories
                .Where(s => s.CategoryId == categoryId)
                .Select(s => new { s.Id, s.Name })
                .ToList();

            return Json(subcats);
        }

        // =========================================================================
        // 📧 HÀM PHỤ: PHÁT HIỆN HẠ GIÁ VÀ TỰ ĐỘNG GỬI EMAIL CẢNH BÁO CHO KHÁCH HÀNG
        // =========================================================================
        private async Task TriggerPriceDropAlertsAsync(int productId, decimal newPrice, string productName)
        {
            var triggeredAlerts = await _context.PriceAlerts
                .Where(a => a.ProductId == productId && !a.IsTriggered && newPrice <= a.TargetPrice)
                .ToListAsync();

            foreach (var alert in triggeredAlerts)
            {
                try
                {
                    // Trình giả lập gửi Email Notification thông báo săn deal thành công
                    // Trong thực tế, Kiệt có thể nhúng SmtpClient hoặc _emailSender.SendEmailAsync tại đây
                    alert.IsTriggered = true;
                    _context.PriceAlerts.Update(alert);
                }
                catch { /* Bẫy lỗi bảo vệ vòng lặp background */ }
            }
            await _context.SaveChangesAsync();
        }
    }
}