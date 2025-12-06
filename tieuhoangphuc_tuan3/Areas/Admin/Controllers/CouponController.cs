using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;     
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")] // nếu bạn có phân quyền role
    public class CouponController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CouponController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Coupon
        public IActionResult Index()
        {
            var coupons = _context.Coupons
                .OrderByDescending(c => c.StartDate)
                .ToList();

            return View(coupons);
        }

        // GET: Admin/Coupon/Add
        public IActionResult Add()
        {
            var model = new Coupon
            {
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(7),
                IsActive = true
            };
            return View(model);
        }

        // POST: Admin/Coupon/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(Coupon model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // có thể check trùng mã
            bool exists = _context.Coupons.Any(c => c.Code == model.Code);
            if (exists)
            {
                ModelState.AddModelError("Code", "Mã giảm giá này đã tồn tại.");
                return View(model);
            }

            _context.Coupons.Add(model);
            _context.SaveChanges();

            TempData["Success"] = "Thêm mã giảm giá thành công.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Coupon/Update/5
        public IActionResult Update(int id)
        {
            var coupon = _context.Coupons.Find(id);
            if (coupon == null)
            {
                return NotFound();
            }

            return View(coupon);
        }

        // POST: Admin/Coupon/Update/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Update(int id, Coupon model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var coupon = _context.Coupons.Find(id);
            if (coupon == null)
            {
                return NotFound();
            }

            // cập nhật các field
            coupon.Code = model.Code;
            coupon.DiscountAmount = model.DiscountAmount;
            coupon.DiscountPercent = model.DiscountPercent;
            coupon.MinOrderValue = model.MinOrderValue;
            coupon.Quantity = model.Quantity;
            coupon.StartDate = model.StartDate;
            coupon.EndDate = model.EndDate;
            coupon.IsActive = model.IsActive;

            _context.SaveChanges();

            TempData["Success"] = "Cập nhật mã giảm giá thành công.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Coupon/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var coupon = _context.Coupons.Find(id);
            if (coupon == null)
            {
                return NotFound();
            }

            _context.Coupons.Remove(coupon);
            _context.SaveChanges();

            TempData["Success"] = "Xóa mã giảm giá thành công.";
            return RedirectToAction(nameof(Index));
        }
    }
}
