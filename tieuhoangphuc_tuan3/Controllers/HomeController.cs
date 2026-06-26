using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebBanDienThoai.Models;
using WebBanDienThoai.Repositories;

namespace WebBanDienThoai.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, IProductRepository productRepository, ApplicationDbContext context)
        {
            _logger = logger;
            _productRepository = productRepository;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // 📦 1. LUỒNG LẤY TẤT CẢ SẢN PHẨM KHÁC (Giữ nguyên cấu trúc gộp SoldCount hiện tại của nhóm)
            var products = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Variants)
                .ToListAsync();

            var productIds = products.Select(p => p.Id).ToList();
            var soldDict = _context.OrderDetails
                .Where(od => productIds.Contains(od.ProductId) && od.Order.Status == OrderStatus.HoanTat)
                .GroupBy(od => od.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(x => x.Quantity) })
                .ToDictionary(g => g.ProductId, g => g.Sold);

            // Đưa về model view cho danh sách sản phẩm thường ngoài trang chủ
            var model = products.Select(p => new ProductWithSoldCount
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

            // ⚡ 2. FIX LỖI COMPILE: Tận dụng danh sách giảm giá mạnh nhất để gán trực tiếp làm Flash Sale
            // Lấy tối đa 8 sản phẩm có DiscountPercent cao nhất để ném vào View thông qua ViewBag
            var topDiscountForFlash = model.OrderByDescending(p => p.DiscountPercent).Take(8).ToList();
            ViewBag.FlashSales = topDiscountForFlash;

            // Thiết lập mốc thời gian kết thúc cố định là 23:59:59 của ngày hôm nay để truyền ra đồng hồ đếm ngược JS
            ViewBag.FlashSaleEndTime = DateTime.Today.AddHours(23).AddMinutes(59).AddSeconds(59).ToString("yyyy-MM-ddTHH:mm:ss");

            // Lấy danh sách sản phẩm nổi bật (IsHot)
            var hotProductModels = model
                .Where(p => p.Id > 0 && products.Any(orig => orig.Id == p.Id && orig.IsHot))
                .Take(8)
                .ToList();

            // Trường hợp DB chưa check IsHot sản phẩm nào, bốc tạm 8 sản phẩm đầu để tránh trống giao diện
            if (!hotProductModels.Any())
            {
                hotProductModels = model.Take(8).ToList();
            }

            ViewBag.HotProducts = hotProductModels;
            return View(model);
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var sitemapBuilder = new StringBuilder();

            sitemapBuilder.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sitemapBuilder.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

            // 1. Thêm các đường dẫn tĩnh (Static Links) cốt lõi của website
            sitemapBuilder.AppendLine($"  <url><loc>{baseUrl}/</loc><priority>1.0</priority></url>");
            sitemapBuilder.AppendLine($"  <url><loc>{baseUrl}/Product</loc><priority>0.8</priority></url>");
            sitemapBuilder.AppendLine($"  <url><loc>{baseUrl}/Order/LoyaltyDashboard</loc><priority>0.7</priority></url>");
            sitemapBuilder.AppendLine($"  <url><loc>{baseUrl}/Blog</loc><priority>0.7</priority></url>");

            // 2. Tự động bốc tất cả các ID sản phẩm thực tế từ Database để dựng URL động chuẩn SEO
            var productIds = await _context.Products
                .AsNoTracking()
                .Select(p => p.Id)
                .ToListAsync();

            foreach (var id in productIds)
            {
                sitemapBuilder.AppendLine("  <url>");
                sitemapBuilder.AppendLine($"    <loc>{baseUrl}/Product/Display/{id}</loc>");
                sitemapBuilder.AppendLine("    <priority>0.6</priority>");
                sitemapBuilder.AppendLine("  </url>");
            }

            sitemapBuilder.AppendLine("</urlset>");

            // Trả về định dạng content XML chuẩn hóa để Google Bot có thể cào dữ liệu lập chỉ mục
            return Content(sitemapBuilder.ToString(), "application/xml", Encoding.UTF8);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult Policy(string type)
        {
            ViewBag.PolicyType = type;
            return View();
        }
    }
}