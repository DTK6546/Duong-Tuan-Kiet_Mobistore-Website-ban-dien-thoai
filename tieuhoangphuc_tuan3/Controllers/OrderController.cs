using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;
using Rotativa.AspNetCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

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
                .Include(o => o.Shipper)
                .Include(o => o.OrderLogs)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            // ⚡ 1. ĐỌC CẤU HÌNH PHÍ SHIP ĐỘNG TỪ BẢNG SHIPPING RATES CỦA ADMIN
            // Mặc định ban đầu nếu không tìm thấy cấu hình quận huyện cụ thể
            decimal shippingFee = order.ShippingFee > 0 ? order.ShippingFee : 30000;
            decimal? freeShipThreshold = 10000000; // Mặc định 10 triệu nếu Admin chưa cấu hình

            // Thử tìm cấu hình Phí ship Admin đặt riêng cho đơn hàng này (Ví dụ dựa vào ghi chú hoặc địa chỉ có chứa mã tỉnh "HCM")
            // Ở đây tụi mình ưu tiên bốc dòng cấu hình đầu tiên hoặc Kiệt có thể khớp theo ProvinceCode nếu có lưu trường đó trong Order
            var rateConfig = await _context.ShippingRates
                .FirstOrDefaultAsync(r => r.ProvinceCode == "HCM"); // Cấu hình mẫu theo HCM trong ảnh của Kiệt

            if (rateConfig != null)
            {
                // Nếu khách chọn giao nhanh thì lấy ExpressFee, giao thường lấy Fee
                shippingFee = order.DeliveryMethod == 1 ? rateConfig.ExpressFee : rateConfig.Fee;
                freeShipThreshold = rateConfig.FreeShipMinOrder;
            }

            // ⚡ 2. TÍNH TOÁN XEM ĐƠN HÀNG ĐẠT MỐC FREESHIP ADMIN ĐẶT RA CHƯA
            decimal subTotal = order.OrderDetails?.Sum(od => od.Price * od.Quantity) ?? 0;
            bool isFreeShip = false;

            // Nếu tổng tiền hàng >= mốc cấu hình FreeShip của Admin (Ví dụ trong ảnh là 8.000.000 đ)
            if (freeShipThreshold.HasValue && subTotal >= freeShipThreshold.Value)
            {
                shippingFee = 0; // Tự động giảm tiền ship về 0đ
                isFreeShip = true;
            }

            ViewBag.SubTotal = subTotal;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.IsFreeShip = isFreeShip;

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .Include(o => o.OrderDetails)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == user.Id);

            if (order == null) return NotFound();

            if (order.Status == OrderStatus.ChoXacNhan || order.Status == OrderStatus.DangXuLy)
            {
                order.Status = OrderStatus.DaHuy;
                _context.Update(order);

                if (order.OrderDetails != null)
                {
                    foreach (var detail in order.OrderDetails)
                    {
                        if (detail.VariantId.HasValue)
                        {
                            var variant = await _context.ProductVariants.FindAsync(detail.VariantId.Value);
                            if (variant != null)
                            {
                                variant.Stock += detail.Quantity;
                                _context.ProductVariants.Update(variant);
                            }
                        }
                        else
                        {
                            var dbProduct = await _context.Products.FindAsync(detail.ProductId);
                            if (dbProduct != null)
                            {
                                dbProduct.Quantity += detail.Quantity;
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

            // ⚡ ĐỒNG BỘ ĐỌC CẤU HÌNH ĐỘNG CHO TRANG XUẤT HÓA ĐƠN
            decimal shippingFee = order.ShippingFee > 0 ? order.ShippingFee : 30000;
            decimal? freeShipThreshold = 10000000;

            var rateConfig = await _context.ShippingRates.FirstOrDefaultAsync(r => r.ProvinceCode == "HCM");
            if (rateConfig != null)
            {
                shippingFee = order.DeliveryMethod == 1 ? rateConfig.ExpressFee : rateConfig.Fee;
                freeShipThreshold = rateConfig.FreeShipMinOrder;
            }

            decimal subTotal = order.OrderDetails?.Sum(od => od.Price * od.Quantity) ?? 0;
            bool isFreeShip = false;

            if (freeShipThreshold.HasValue && subTotal >= freeShipThreshold.Value)
            {
                shippingFee = 0;
                isFreeShip = true;
            }

            ViewBag.SubTotal = subTotal;
            ViewBag.ShippingFee = shippingFee;
            ViewBag.IsFreeShip = isFreeShip;

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
        // 🎁 1. TRANG LOYALTY DASHBOARD: THỂ THÀNH VIÊN VÀ QUY ĐỔI ĐIỂM
        // =========================================================================
        [Authorize]
        public async Task<IActionResult> LoyaltyDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // =========================================================================
            // 📊 PHÂN HỆ CRM: BỔ SUNG LOGIC QUÉT DATABASE PHÂN TÍCH LỊCH SỬ TIÊU DÙNG
            // =========================================================================

            // 1. Lấy toàn bộ đơn hàng của khách để tính chỉ số
            var userOrders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.UserId == user.Id)
                .Include(o => o.OrderDetails)
                .ToListAsync();

            var totalOrdersCount = userOrders.Count;
            var successOrdersCount = userOrders.Count(o => o.Status == OrderStatus.HoanTat);

            // 2. Phân tích tìm sản phẩm khách chi tiền mua nhiều nhất
            var favoriteProduct = userOrders
                .Where(o => o.Status == OrderStatus.HoanTat)
                .SelectMany(o => o.OrderDetails)
                .GroupBy(d => d.ProductName)
                .Select(g => new { Name = g.Key, Count = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.Count)
                .FirstOrDefault()?.Name ?? "Chưa có dữ liệu";

            // 3. Thống kê số tiền chi trả theo 12 tháng trong năm 2026 để vẽ biểu đồ tần suất
            var monthlyData = new decimal[12];
            foreach (var o in userOrders.Where(o => o.Status == OrderStatus.HoanTat && o.OrderDate.Year == 2026))
            {
                int monthIndex = o.OrderDate.Month - 1; // 0 -> 11
                if (monthIndex >= 0 && monthIndex < 12)
                {
                    monthlyData[monthIndex] += o.TotalPrice;
                }
            }

            // Gửi các chỉ số CRM mới ra ViewBag
            ViewBag.TotalOrdersCount = totalOrdersCount;
            ViewBag.SuccessOrdersCount = successOrdersCount;
            ViewBag.SuccessRate = totalOrdersCount > 0 ? Math.Round((double)successOrdersCount / totalOrdersCount * 100, 1) : 0;
            ViewBag.FavoriteProduct = favoriteProduct;
            ViewBag.MonthlyDataJson = System.Text.Json.JsonSerializer.Serialize(monthlyData);

            // =========================================================================
            // 🏅 GIỮ NGUYÊN LOGIC PHÂN CẤP VÀ ĐỔI THƯỞNG CỦA KIỆT
            // =========================================================================
            string memberLevel = "Thành viên Đồng (Bronze)";
            string cardClass = "bg-secondary text-white";
            if (user.RankingPoints >= 50000) { memberLevel = "Thành viên Kim Cương (Diamond)"; cardClass = "bg-dark text-warning border border-warning"; }
            else if (user.RankingPoints >= 35000)
            {
                memberLevel = "Thành viên Bạch Kim (Platinum)";
                cardClass = "bg-info bg-gradient text-white border border-light shadow-lg";
            }
            else if (user.RankingPoints >= 20000) { memberLevel = "Thành viên Vàng (Gold)"; cardClass = "bg-warning text-dark fw-bold"; }
            else if (user.RankingPoints >= 10000) { memberLevel = "Thành viên Bạc (Silver)"; cardClass = "bg-info text-white"; }

            bool isBirthdayToday = true; // Giữ nguyên flag ngày sinh nhật của Kiệt
            ViewBag.IsBirthdayToday = isBirthdayToday;

            var rewards = await _context.LoyaltyRewards.Where(r => r.IsActive).ToListAsync();

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

            user.CurrentPoints -= reward.PointsRequired;
            _context.Update(user);

            string generatedCouponCode = $"{reward.CouponCodePrefix ?? "LYL"}{Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper()}";

            var newCoupon = new Coupon
            {
                Code = generatedCouponCode,
                DiscountAmount = reward.DiscountAmount,
                Quantity = 1,
                CurrentUsage = 0,
                IsActive = true,
                StartDate = DateTime.Now.AddMinutes(-5),
                EndDate = DateTime.Now.AddDays(30),
                MinOrderValue = 0m,
            };

            _context.Coupons.Add(newCoupon);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đổi quà thành công! Bạn đã đổi {reward.PointsRequired} điểm lấy mã giảm giá: {generatedCouponCode} (Trị giá {reward.DiscountAmount.ToString("N0")} ₫)";
            return RedirectToAction(nameof(LoyaltyDashboard));
        }
    }
}