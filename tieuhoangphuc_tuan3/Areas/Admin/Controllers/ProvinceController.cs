using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ProvinceController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ProvinceController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index()
        {
            var items = await _context.Provinces
                .OrderBy(x => x.Name)
                .ToListAsync();
            return View(items);
        }

        public IActionResult Create() => View(new Province());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Province model)
        {
            model.Code = (model.Code ?? "").Trim().ToUpper();
            model.Name = (model.Name ?? "").Trim();

            if (!ModelState.IsValid) return View(model);

            bool exists = await _context.Provinces.AnyAsync(x => x.Code == model.Code);
            if (exists)
            {
                ModelState.AddModelError("", "Mã Tỉnh/TP đã tồn tại.");
                return View(model);
            }

            _context.Provinces.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.Provinces.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Province model)
        {
            if (id != model.Id) return BadRequest();

            // Lấy bản ghi hiện tại để biết oldCode
            var current = await _context.Provinces.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (current == null) return NotFound();

            var oldCode = (current.Code ?? "").Trim().ToUpper();

            model.Code = (model.Code ?? "").Trim().ToUpper();
            model.Name = (model.Name ?? "").Trim();

            if (!ModelState.IsValid) return View(model);

            bool exists = await _context.Provinces.AnyAsync(x => x.Id != model.Id && x.Code == model.Code);
            if (exists)
            {
                ModelState.AddModelError("", "Mã Tỉnh/TP đã tồn tại.");
                return View(model);
            }

            // Nếu đổi Province.Code -> update ShippingRates liên quan để không bị mất map
            if (oldCode != model.Code)
            {
                var rates = await _context.ShippingRates
                    .Where(r => r.ProvinceCode == oldCode)
                    .ToListAsync();

                foreach (var r in rates)
                    r.ProvinceCode = model.Code;
            }

            _context.Update(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Provinces.FindAsync(id);
            if (item == null) return NotFound();

            // Chặn xóa nếu còn District
            bool hasDistrict = await _context.Districts.AnyAsync(d => d.ProvinceId == id);
            if (hasDistrict)
            {
                TempData["Error"] = "Không thể xóa Tỉnh/TP vì còn Quận/Huyện liên quan.";
                return RedirectToAction(nameof(Index));
            }

            // Chặn xóa nếu còn ShippingRate dùng ProvinceCode này
            var code = (item.Code ?? "").Trim().ToUpper();
            bool hasShippingRate = await _context.ShippingRates.AnyAsync(r => r.ProvinceCode == code);
            if (hasShippingRate)
            {
                TempData["Error"] = "Không thể xóa Tỉnh/TP vì còn cấu hình phí vận chuyển (ShippingRate) liên quan.";
                return RedirectToAction(nameof(Index));
            }

            _context.Provinces.Remove(item);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
