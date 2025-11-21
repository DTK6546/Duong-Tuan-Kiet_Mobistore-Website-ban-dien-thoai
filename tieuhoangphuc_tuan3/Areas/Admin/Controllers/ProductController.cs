using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;
using WebBanDienThoai.Repositories;
using WebBanDienThoai.Services.SignalR; // ChatHub

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class ProductController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _chatHub;     // 🔔

        public ProductController(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository,
            ApplicationDbContext context,
            IHubContext<ChatHub> chatHub)                   // 🔔
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _context = context;
            _chatHub = chatHub;                             // 🔔
        }

        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        public async Task<IActionResult> Index(string searchTerm, string sortOrder)
        {
            var products = await _productRepository.GetAllAsync();

            // --- Tìm kiếm ---
            if (!string.IsNullOrEmpty(searchTerm))
            {
                products = products.Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                                               p.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // --- Sắp xếp ---
            switch (sortOrder)
            {
                case "price_asc":
                    products = products.OrderBy(p => p.Price).ToList();
                    break;
                case "price_desc":
                    products = products.OrderByDescending(p => p.Price).ToList();
                    break;
            }

            // --- Cảnh báo tồn kho thấp ---
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
                // Ảnh sản phẩm
                if (imageUrl != null)
                {
                    product.ImageUrl = await SaveImage(imageUrl);
                }

                // Tính giá sau giảm
                if (product.Price > 0 && product.DiscountPercent > 0)
                    product.DiscountedPrice = product.Price - (product.Price * product.DiscountPercent / 100);
                else
                    product.DiscountedPrice = product.Price;

                // Cập nhật ngày nhập hàng
                product.LastImportDate = DateTime.Now;

                await _productRepository.AddAsync(product);

                // 🔔 PUSH: Sản phẩm mới
                var url = Url.Action("Display", "Product", new { area = "", id = product.Id }, Request.Scheme);
                var summary = $"Giá: {product.DiscountedPrice:N0}₫" +
                              (product.DiscountPercent > 0 ? $" (Giảm {product.DiscountPercent}%)" : "");

                await _chatHub.Clients.All.SendAsync(
                    "ReceiveProductNotification",
                    $"Sản phẩm mới: {product.Name}",            // title
                    summary,                                    // summary
                    url,                                        // url xem chi tiết
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm")   // time
                );

                TempData["SuccessMessage"] = "Sản phẩm đã được thêm thành công!";
                return RedirectToAction(nameof(Index));
            }

            // Nếu lỗi
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
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        [Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employer)]
        public async Task<IActionResult> Update(int id)
        {
            var product = await _productRepository.GetByIdAsync(id);
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
        public async Task<IActionResult> Update(int id, Product product, IFormFile imageUrl, IFormFile[] additionalImages)
        {
            ModelState.Remove("ImageUrl");
            if (id != product.Id)
                return NotFound();

            if (ModelState.IsValid)
            {
                var existingProduct = await _productRepository.GetByIdAsync(id);
                if (existingProduct == null)
                    return NotFound();

                // Ảnh
                if (imageUrl != null)
                    product.ImageUrl = await SaveImage(imageUrl);
                else
                    product.ImageUrl = existingProduct.ImageUrl;

                // Giá sau giảm
                if (product.Price > 0 && product.DiscountPercent > 0)
                    product.DiscountedPrice = product.Price - (product.Price * product.DiscountPercent / 100);
                else
                    product.DiscountedPrice = product.Price;

                // Cập nhật dữ liệu
                existingProduct.Name = product.Name;
                existingProduct.Price = product.Price;
                existingProduct.Description = product.Description;
                existingProduct.DiscountPercent = product.DiscountPercent;
                existingProduct.CategoryId = product.CategoryId;
                existingProduct.SubCategoryId = product.SubCategoryId;
                existingProduct.ImageUrl = product.ImageUrl;
                existingProduct.DiscountedPrice = product.DiscountedPrice;

                // 🧭 Kiểm tra thay đổi số lượng → cập nhật ngày nhập
                if (product.Quantity > existingProduct.Quantity)
                    existingProduct.LastImportDate = DateTime.Now;

                existingProduct.Quantity = product.Quantity;
                existingProduct.MinStockLevel = product.MinStockLevel;

                await _productRepository.UpdateAsync(existingProduct);

                // 🔔 PUSH: Cập nhật sản phẩm
                var url = Url.Action("Display", "Product", new { area = "", id = existingProduct.Id }, Request.Scheme);
                var summary = $"Giá: {existingProduct.DiscountedPrice:N0}₫" +
                              (existingProduct.DiscountPercent > 0 ? $" (Giảm {existingProduct.DiscountPercent}%)" : "");

                await _chatHub.Clients.All.SendAsync(
                    "ReceiveProductNotification",
                    $"Cập nhật: {existingProduct.Name}",         // title
                    summary,                                    // summary
                    url,                                        // url
                    DateTime.Now.ToString("dd/MM/yyyy HH:mm")   // time
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
            var product = await _productRepository.GetByIdAsync(id);
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

            await _productRepository.DeleteAsync(id);

            // 🔔 PUSH: Xóa sản phẩm (không có url)
            await _chatHub.Clients.All.SendAsync(
                "ReceiveProductNotification",
                $"Đã xóa: {name}",
                "Sản phẩm đã bị xóa khỏi hệ thống.",
                null,
                DateTime.Now.ToString("dd/MM/yyyy HH:mm")
            );

            TempData["SuccessMessage"] = "Sản phẩm và ảnh liên kết đã được xóa thành công!";
            return RedirectToAction(nameof(Index));
        }

        // 🖼️ Lưu ảnh
        private async Task<string> SaveImage(IFormFile image)
        {
            var savePath = Path.Combine("wwwroot/images", image.FileName);
            using (var fileStream = new FileStream(savePath, FileMode.Create))
            {
                await image.CopyToAsync(fileStream);
            }
            return "/images/" + image.FileName;
        }

        // 🧩 Lấy SubCategory động
        [HttpGet]
        public JsonResult GetSubCategories(int categoryId)
        {
            var subcats = _context.SubCategories
                .Where(s => s.CategoryId == categoryId)
                .Select(s => new { s.Id, s.Name })
                .ToList();

            return Json(subcats);
        }
    }
}
