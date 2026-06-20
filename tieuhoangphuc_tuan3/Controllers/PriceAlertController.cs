using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Controllers
{
    [Authorize] // Bắt buộc đăng nhập để sử dụng tính năng theo dõi giá
    public class PriceAlertController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PriceAlertController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================================================================
        // 🔔 API ĐĂNG KÝ THEO DÕI BIẾN ĐỘNG GIÁ - ĐỒNG BỘ 100% THEO DATABASE
        // =========================================================================
        [HttpPost]
        public async Task<IActionResult> RegisterAlert(int productId, decimal targetPrice)
        {
            if (productId <= 0 || targetPrice <= 0)
            {
                return Json(new { success = false, message = "Mức giá mong muốn không hợp lệ." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập hệ thống để thực hiện." });
            }

            // Kiểm tra xem sản phẩm có tồn tại không
            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId);
            if (product == null)
            {
                return Json(new { success = false, message = "Sản phẩm không tồn tại." });
            }

            // Bẫy logic: Nếu giá kỳ vọng cao hơn hoặc bằng giá hiện tại
            if (targetPrice >= product.DiscountedPrice)
            {
                return Json(new { success = false, message = $"Giá hiện tại đang là {product.DiscountedPrice:N0} đ, đã thấp hơn mức giá kỳ vọng của bạn rồi!" });
            }

            // Kiểm tra xem tài khoản này đã đăng ký theo dõi sản phẩm này chưa
            var existingAlert = await _context.PriceAlerts
                .FirstOrDefaultAsync(a => a.ProductId == productId && a.UserId == user.Id);

            if (existingAlert != null)
            {
                // Nếu tồn tại rồi thì cập nhật lại mốc giá và reset trạng thái kích hoạt thông báo
                existingAlert.TargetPrice = targetPrice;
                existingAlert.CreatedDate = DateTime.Now; // Map đúng trường CreatedDate trong hình 
                existingAlert.IsTriggered = false;       // Map đúng trường IsTriggered trong hình
                existingAlert.Email = user.Email;         // Cập nhật lại Email nếu có thay đổi

                _context.PriceAlerts.Update(existingAlert);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Đã cập nhật lại giá kỳ vọng theo dõi thành: {targetPrice:N0} VNĐ." });
            }

            // Tiến hành khởi tạo bản ghi mới khớp hoàn toàn các trường trong Database của nhóm
            var priceAlert = new PriceAlert
            {
                UserId = user.Id,
                ProductId = productId,
                TargetPrice = targetPrice,
                Email = user.Email,             // Lưu Email để Background Task quét gửi thư sau này
                IsTriggered = false,            // Chưa được kích hoạt gửi thông báo
                CreatedDate = DateTime.Now      // Thời gian khởi tạo bản ghi
            };

            _context.PriceAlerts.Add(priceAlert);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"✓ Đăng ký theo dõi giá thành công! MobiStore sẽ gửi thông báo ngay khi giá giảm xuống dưới {targetPrice:N0} VNĐ." });
        }
    }
}