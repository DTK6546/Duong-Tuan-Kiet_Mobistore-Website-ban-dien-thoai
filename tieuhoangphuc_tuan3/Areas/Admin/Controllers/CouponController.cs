using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CouponController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CouponController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Coupon
        public async Task<IActionResult> Index()
        {
            var coupons = await _context.Coupons
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();

            return View(coupons);
        }

        // GET: Admin/Coupon/Add
        public IActionResult Add()
        {
            var model = new Coupon
            {
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(7),
                IsActive = true,
                MinOrderValue = 0,
                Quantity = 0,
                CurrentUsage = 0
            };
            return View(model);
        }

        // POST: Admin/Coupon/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Coupon model)
        {
            NormalizeCoupon(model);
            ValidateCoupon(model);

            if (!ModelState.IsValid)
                return View(model);

            // check trùng mã (case-insensitive)
            bool exists = await _context.Coupons
                .AnyAsync(c => c.Code.ToUpper() == model.Code);

            if (exists)
            {
                ModelState.AddModelError("Code", "Mã giảm giá này đã tồn tại.");
                return View(model);
            }

            // đảm bảo CurrentUsage không bị nhập bậy
            if (model.CurrentUsage < 0) model.CurrentUsage = 0;
            if (model.Quantity < 0) model.Quantity = 0;

            _context.Coupons.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Thêm mã giảm giá thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Coupon/Update/5
        public async Task<IActionResult> Update(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();
            return View(coupon);
        }

        // POST: Admin/Coupon/Update/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, Coupon model)
        {
            if (id != model.Id) return NotFound();

            NormalizeCoupon(model);
            ValidateCoupon(model);

            if (!ModelState.IsValid)
                return View(model);

            // check trùng mã (case-insensitive) - loại trừ chính nó
            bool exists = await _context.Coupons
                .AnyAsync(c => c.Id != model.Id && c.Code.ToUpper() == model.Code);

            if (exists)
            {
                ModelState.AddModelError("Code", "Mã giảm giá này đã tồn tại.");
                return View(model);
            }

            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();

            // cập nhật field
            coupon.Code = model.Code;
            coupon.DiscountAmount = model.DiscountAmount;
            coupon.DiscountPercent = model.DiscountPercent;
            coupon.MinOrderValue = model.MinOrderValue;
            coupon.Quantity = model.Quantity;
            coupon.StartDate = model.StartDate;
            coupon.EndDate = model.EndDate;
            coupon.IsActive = model.IsActive;

            // KHÔNG cho sửa CurrentUsage từ form (để hệ thống tự tăng khi khách dùng)
            // coupon.CurrentUsage giữ nguyên

            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật mã giảm giá thành công.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Coupon/Delete/5  (Soft disable)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();

            coupon.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Đã ngưng mã giảm giá (không xóa dữ liệu).";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Coupon/UsedByCustomers/5
        public async Task<IActionResult> UsedByCustomers(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id);
            if (coupon == null) return NotFound();

            var usedCoupons = await _context.CouponUsages
                .Where(cu => cu.CouponId == coupon.Id)
                .Include(cu => cu.User)
                .OrderByDescending(cu => cu.UsedAt)
                .ToListAsync();

            return View(usedCoupons);
        }

        // ----------------- Helpers -----------------

        private static void NormalizeCoupon(Coupon model)
        {
            model.Code = (model.Code ?? "").Trim().ToUpper();
        }

        private void ValidateCoupon(Coupon model)
        {
            if (string.IsNullOrWhiteSpace(model.Code))
                ModelState.AddModelError("Code", "Vui lòng nhập mã giảm giá.");

            // chỉ được dùng 1 trong 2
            if (model.DiscountAmount.HasValue && model.DiscountPercent.HasValue)
                ModelState.AddModelError("", "Chỉ được nhập GIẢM TIỀN hoặc GIẢM %, không được nhập cả hai.");

            if (!model.DiscountAmount.HasValue && !model.DiscountPercent.HasValue)
                ModelState.AddModelError("", "Vui lòng nhập GIẢM TIỀN hoặc GIẢM %.");

            // validate %
            if (model.DiscountPercent.HasValue && (model.DiscountPercent < 1 || model.DiscountPercent > 100))
                ModelState.AddModelError("DiscountPercent", "Giảm % phải từ 1 đến 100.");

            // validate tiền
            if (model.DiscountAmount.HasValue && model.DiscountAmount <= 0)
                ModelState.AddModelError("DiscountAmount", "Số tiền giảm phải > 0.");

            if (model.MinOrderValue < 0)
                ModelState.AddModelError("MinOrderValue", "Đơn tối thiểu không được âm.");

            if (model.Quantity < 0)
                ModelState.AddModelError("Quantity", "Số lượt sử dụng không được âm.");

            // date
            if (model.EndDate <= model.StartDate)
                ModelState.AddModelError("", "Ngày kết thúc phải lớn hơn ngày bắt đầu.");

            // usage không được vượt quá quantity (nếu quantity > 0)
            if (model.Quantity > 0 && model.CurrentUsage > model.Quantity)
                ModelState.AddModelError("", "CurrentUsage không được lớn hơn Quantity.");
        }
    }
}
