using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using WebBanDienThoai.Models;
using WebBanDienThoai.Extensions;
using Microsoft.EntityFrameworkCore;

namespace WebBanDienThoai.Controllers
{
    public class TradeInController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TradeInController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public IActionResult Form(int productId)
        {
            var product = _context.Products.Find(productId);
            if (product == null) return NotFound();

            ViewBag.TargetProduct = product;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Calculate(string oldDeviceName, string cosmetic, string functionality, int targetProductId)
        {
            if (string.IsNullOrWhiteSpace(oldDeviceName))
            {
                return Json(new { success = false, message = "Vui lòng nhập tên thiết bị cũ!" });
            }

            // =========================================================================
            // 🌟 THUẬT TOÁN ĐỊNH GIÁ TỰ ĐỘNG CHI TIẾT THEO PHÂN KHÚC & ĐỜI MÁY
            // =========================================================================
            decimal baseValue = 10000000m; // Giá sàn mặc định ban đầu cho phân khúc tầm trung
            string nameLower = oldDeviceName.ToLower().Trim();

            // 1. Nhóm siêu phẩm đời mới nhất (iPhone 15 Series, Galaxy S24 Series...)
            if (nameLower.Contains("iphone 15") || nameLower.Contains("s24") || nameLower.Contains("z fold6") || nameLower.Contains("z flip6"))
            {
                baseValue = 18000000m;
                if (nameLower.Contains("pro max") || nameLower.Contains("ultra")) baseValue = 24000000m;
                else if (nameLower.Contains("plus") || nameLower.Contains("pro")) baseValue = 20000000m;
            }
            // 2. Nhóm cận cao cấp (iPhone 13, iPhone 14 Series, Galaxy S23...)
            else if (nameLower.Contains("iphone 14") || nameLower.Contains("iphone 13") || nameLower.Contains("s23"))
            {
                baseValue = 12000000m;
                if (nameLower.Contains("pro max") || nameLower.Contains("ultra")) baseValue = 16000000m;
                else if (nameLower.Contains("plus") || nameLower.Contains("pro")) baseValue = 14000000m;
            }
            // 3. Nhóm các dòng máy đời cũ sâu hẳn (iPhone 7, iPhone 8, iPhone X...)
            else if (nameLower.Contains("iphone 7") || nameLower.Contains("iphone 8") || nameLower.Contains("iphone x"))
            {
                baseValue = 3500000m;
                if (nameLower.Contains("plus")) baseValue = 4500000m; // Bản Plus nhỉnh hơn một chút
            }
            // 4. Bẫy bắt từ khóa chung chung cho các dòng máy khác không thuộc danh sách trên
            else if (nameLower.Contains("pro max") || nameLower.Contains("ultra"))
            {
                baseValue = 15000000m;
            }
            else if (nameLower.Contains("plus") || nameLower.Contains("pro"))
            {
                baseValue = 11000000m;
            }

            // =========================================================================
            // 📉 TÍNH TOÁN HỆ SỐ HAO MÒN (Giữ nguyên cấu trúc map dữ liệu từ Form gửi lên)
            // =========================================================================
            decimal cosmeticFactor = cosmetic switch
            {
                "A" => 1.0m,  // Máy đẹp như mới
                "B" => 0.8m,  // Trầy xước nhẹ
                "C" => 0.5m,  // Cấn móp nặng
                _ => 0.3m
            };

            decimal funcFactor = functionality switch
            {
                "Good" => 1.0m,       // Hoạt động hoàn hảo
                "MinorFault" => 0.7m,  // Lỗi tính năng nhẹ (chai pin, hỏng loa...)
                "ScreenFault" => 0.4m, // Lỗi màn hình (sọc, ám ố...)
                _ => 0.1m
            };

            // Tính toán mức giá cuối cùng (Làm tròn không lấy số lẻ thập phân)
            decimal finalEstimatedValue = Math.Round(baseValue * cosmeticFactor * funcFactor, 0);

            // Ghi nhận nhãn hiển thị thân thiện để lưu vào DB (Dành cho trang quản trị Admin hiển thị trực quan)
            string displayCosmetic = cosmetic == "A" ? "Loại A" : (cosmetic == "B" ? "Loại B" : "Loại C");

            // Khởi tạo thực thể TradeIn và chuẩn bị lưu vết
            var tradeInRequest = new TradeIn
            {
                UserId = _userManager.GetUserId(User) ?? "Guest",
                OldDeviceName = oldDeviceName.Trim(),
                CosmeticCondition = displayCosmetic, // Lưu "Loại B" thay vì lưu chữ "B" cộc lốc
                Functionality = functionality,
                EstimatedValue = finalEstimatedValue,
                TargetProductId = targetProductId,
                CreatedAt = DateTime.Now,
                IsApplied = false
            };

            _context.TradeIns.Add(tradeInRequest);
            await _context.SaveChangesAsync();

            // 💾 LƯU GIÁ TRỊ GIẢM TRỪ VÀO SESSION ĐỂ TRANG CHECKOUT ĐỒNG BỘ KHẤU TRỪ
            HttpContext.Session.SetInt32("TradeInId", tradeInRequest.Id);
            HttpContext.Session.SetString("TradeInDiscount", finalEstimatedValue.ToString());

            return Json(new { success = true, estimatedValue = finalEstimatedValue, tradeInId = tradeInRequest.Id });
        }
    }
}