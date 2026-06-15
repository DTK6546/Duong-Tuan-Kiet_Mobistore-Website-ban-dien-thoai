using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;
using Rotativa.AspNetCore;

namespace WebBanDienThoai.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OrderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> MyOrders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == user.Id)
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id && o.UserId == user.Id)
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.Shipper) // ✨ BỔ SUNG: Nạp thông tin Shipper đảm nhận đơn hàng
                .Include(o => o.OrderLogs) // ✨ BỔ SUNG: Nạp nhật ký hành trình vận chuyển
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Include thêm cả OrderDetails để lấy danh sách sản phẩm cần trả kho
            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();

            // Chỉ cho huỷ khi đơn chưa đi giao
            if (order.Status == OrderStatus.ChoXacNhan || order.Status == OrderStatus.DangXuLy)
            {
                order.Status = OrderStatus.DaHuy;
                _context.Update(order);

                // 🧭 HOÀN LẠI SỐ LƯỢNG SẢN PHẨM VÀO KHO KHI HỦY ĐƠN
                if (order.OrderDetails != null)
                {
                    foreach (var detail in order.OrderDetails)
                    {
                        if (detail.VariantId.HasValue)
                        {
                            var variant = await _context.ProductVariants.FindAsync(detail.VariantId.Value);
                            if (variant != null)
                            {
                                variant.Stock += detail.Quantity; // Cộng trả lại kho biến thể
                                _context.ProductVariants.Update(variant);
                            }
                        }
                        else
                        {
                            var dbProduct = await _context.Products.FindAsync(detail.ProductId);
                            if (dbProduct != null)
                            {
                                dbProduct.Quantity += detail.Quantity; // Cộng trả lại kho sản phẩm chính
                                _context.Products.Update(dbProduct);
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReceived(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);
            if (order == null) return NotFound();

            if (order.Status == OrderStatus.DaGiao)
            {
                order.Status = OrderStatus.HoanTat;
                _context.Update(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnOrder(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);
            if (order == null) return NotFound();

            // Chỉ cho trả hàng khi đã hoàn tất
            if (order.Status == OrderStatus.HoanTat)
            {
                order.Status = OrderStatus.TraHang;
                _context.Update(order);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Invoice(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .Where(o => o.Id == id && o.UserId == user.Id)
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            return View(order);
        }

        public async Task<IActionResult> ExportInvoicePdf(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .Where(o => o.Id == id && o.UserId == user.Id)
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            return new ViewAsPdf("Invoice", order)
            {
                FileName = $"HoaDon_{order.Id}.pdf"
            };
        }

        // =========================================================================
        // 🎁 1. TRANG LOYALTY DASHBOARD: THẺ THÀNH VIÊN VÀ QUY ĐỔI ĐIỂM
        // =========================================================================
        public async Task<IActionResult> LoyaltyDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // 💳 TỰ ĐỘNG TÍNH HẠNG THÀNH VIÊN THEO ĐIỂM TÍCH LŨY (RankingPoints)
            string memberLevel = "Thành viên Đồng (Bronze)";
            string cardClass = "bg-secondary text-white";
            if (user.RankingPoints >= 50000) { memberLevel = "Thành viên Kim Cương (Diamond)"; cardClass = "bg-dark text-warning border border-warning"; }
            else if (user.RankingPoints >= 35000) // Mốc mới cho Bạch Kim
            {
                memberLevel = "Thành viên Bạch Kim (Platinum)";
                cardClass = "bg-info bg-gradient text-white border border-light shadow-lg"; // Thẻ xanh lam ánh bạc metallic
            }
            else if (user.RankingPoints >= 20000) { memberLevel = "Thành viên Vàng (Gold)"; cardClass = "bg-warning text-dark fw-bold"; }
            else if (user.RankingPoints >= 10000) { memberLevel = "Thành viên Bạc (Silver)"; cardClass = "bg-info text-white"; }

            // 🎯 BIRTHDAY BONUS: Check nếu hôm nay là sinh nhật khách hàng (Giả lập cộng 50 điểm)
            bool isBirthdayToday = true;
            ViewBag.IsBirthdayToday = isBirthdayToday;

            // Lấy danh sách quà tặng đang mở cấu hình đổi điểm
            var rewards = await _context.LoyaltyRewards.Where(r => r.IsActive).ToListAsync();

            // Nếu DB trống, tự động nạp vài phần quà giả lập để chạy thử nghiệm nghiệm thu
            if (!rewards.Any())
            {
                rewards = new List<LoyaltyReward>
                {
                    new LoyaltyReward { Id = 1, Title = "Voucher Ưu đãi 50.000 ₫", PointsRequired = 50, DiscountAmount = 50000 },
                    new LoyaltyReward { Id = 2, Title = "Voucher Đặc quyền 200.000 ₫", PointsRequired = 150, DiscountAmount = 200000 },
                    new LoyaltyReward { Id = 3, Title = "Voucher VIP MobiStore 500.000 ₫", PointsRequired = 300, DiscountAmount = 500000 }
                };
            }

            ViewBag.MemberLevel = memberLevel;
            ViewBag.CardClass = cardClass;
            ViewBag.CurrentPoints = user.CurrentPoints;
            ViewBag.RankingPoints = user.RankingPoints;

            return View(rewards);
        }

        // =========================================================================
        // 🎁 2. LOGIC ĐỔI ĐIỂM → TỰ ĐỘNG SINH VOUCHER GIẢM GIÁ GỐC CỦA BẠN
        // =========================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RedeemPoints(int rewardId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // Giả lập danh sách quà để đối chiếu nếu DB trống
            var reward = await _context.LoyaltyRewards.FirstOrDefaultAsync(r => r.Id == rewardId);
            if (reward == null)
            {
                var fallbackRewards = new List<LoyaltyReward>
                {
                    new LoyaltyReward { Id = 1, Title = "Voucher Ưu đãi 50.000 ₫", PointsRequired = 50, DiscountAmount = 50000 },
                    new LoyaltyReward { Id = 2, Title = "Voucher Đặc quyền 200.000 ₫", PointsRequired = 150, DiscountAmount = 200000 },
                    new LoyaltyReward { Id = 3, Title = "Voucher VIP MobiStore 500.000 ₫", PointsRequired = 300, DiscountAmount = 500000 }
                };
                reward = fallbackRewards.FirstOrDefault(r => r.Id == rewardId);
            }

            if (reward == null) { TempData["ErrorMessage"] = "Phần thưởng không tồn tại."; return RedirectToAction(nameof(LoyaltyDashboard)); }

            if (user.CurrentPoints < reward.PointsRequired)
            {
                TempData["ErrorMessage"] = $"Bạn không đủ điểm để đổi. Cần tối thiểu {reward.PointsRequired} điểm.";
                return RedirectToAction(nameof(LoyaltyDashboard));
            }

            // 1. Thực hiện trừ điểm ví tiêu dùng của khách (Điểm thứ hạng RankingPoints giữ nguyên để bảo lưu cấp thẻ)
            user.CurrentPoints -= reward.PointsRequired;
            _context.Update(user);

            // 2. Tự động đâm thẳng 1 bản ghi mã giảm giá mới tinh vào bảng Coupon có sẵn trong DB của bạn
            string generatedCouponCode = $"{reward.CouponCodePrefix ?? "LYL"}{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";

            var newCoupon = new Coupon
            {
                Code = generatedCouponCode,
                DiscountAmount = reward.DiscountAmount,
                Quantity = 1,
                CurrentUsage = 0, // Đảm bảo lượt dùng khởi tạo bằng 0
                IsActive = true,

                // Sửa ở đây: Đồng bộ dùng DateTime.Now (hoặc đổi hết sang UtcNow tùy cấu hình DB của bạn)
                StartDate = DateTime.Now.AddMinutes(-5), // Lùi lại 5 phút để tránh lag giờ hệ thống
                EndDate = DateTime.Now.AddDays(30),       // Hạn dùng 30 ngày kể từ lúc đổi

                MinOrderValue = 0m, // Gán bằng 0 để đổi điểm xong là chắc chắn dùng được luôn
            };

            _context.Coupons.Add(newCoupon);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đổi quà thành công! Bạn đã đổi {reward.PointsRequired} điểm lấy mã giảm giá: {generatedCouponCode} (Trị giá {reward.DiscountAmount.ToString("N0")} ₫)";
            return RedirectToAction(nameof(LoyaltyDashboard));
        }
    }
}
