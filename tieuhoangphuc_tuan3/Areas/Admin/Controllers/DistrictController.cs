using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DistrictController : Controller
    {
        private readonly ApplicationDbContext _context;
        public DistrictController(ApplicationDbContext context) => _context = context;

        public async Task<IActionResult> Index(int? provinceId)
        {
            var query = _context.Districts.Include(d => d.Province).AsQueryable();

            if (provinceId.HasValue)
                query = query.Where(d => d.ProvinceId == provinceId.Value);

            var items = await query
                .OrderBy(d => d.Province!.Name)
                .ThenBy(d => d.Name)
                .ToListAsync();

            ViewBag.ProvinceId = provinceId;
            ViewBag.Provinces = new SelectList(await _context.Provinces.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", provinceId);

            return View(items);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Provinces = new SelectList(await _context.Provinces.OrderBy(p => p.Name).ToListAsync(), "Id", "Name");
            return View(new District());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(District model)
        {
            model.Code = (model.Code ?? "").Trim().ToUpper();
            model.Name = (model.Name ?? "").Trim();

            ViewBag.Provinces = new SelectList(await _context.Provinces.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", model.ProvinceId);

            if (!ModelState.IsValid) return View(model);

            bool exists = await _context.Districts.AnyAsync(x => x.ProvinceId == model.ProvinceId && x.Code == model.Code);
            if (exists)
            {
                ModelState.AddModelError("", "Mã Quận/Huyện đã tồn tại trong Tỉnh/TP này.");
                return View(model);
            }

            _context.Districts.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { provinceId = model.ProvinceId });
        }

        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.Districts.FindAsync(id);
            if (item == null) return NotFound();

            ViewBag.Provinces = new SelectList(await _context.Provinces.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", item.ProvinceId);
            return View(item);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, District model)
        {
            if (id != model.Id) return BadRequest();

            // Lấy bản ghi hiện tại để biết oldCode/oldProvinceId
            var current = await _context.Districts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
            if (current == null) return NotFound();

            var oldCode = (current.Code ?? "").Trim().ToUpper();
            var oldProvinceId = current.ProvinceId;

            model.Code = (model.Code ?? "").Trim().ToUpper();
            model.Name = (model.Name ?? "").Trim();

            ViewBag.Provinces = new SelectList(await _context.Provinces.OrderBy(p => p.Name).ToListAsync(), "Id", "Name", model.ProvinceId);

            if (!ModelState.IsValid) return View(model);

            bool exists = await _context.Districts.AnyAsync(x =>
                x.Id != model.Id && x.ProvinceId == model.ProvinceId && x.Code == model.Code);

            if (exists)
            {
                ModelState.AddModelError("", "Mã Quận/Huyện đã tồn tại trong Tỉnh/TP này.");
                return View(model);
            }

            // Nếu đổi District.Code -> update ShippingRates tương ứng để Quote không bị mất
            // Lưu ý: ShippingRate đang lưu ProvinceCode (string), nên cần lấy provinceCode của district hiện tại (oldProvinceId)
            if (oldCode != model.Code || oldProvinceId != model.ProvinceId)
            {
                var oldProvinceCode = await _context.Provinces
                    .Where(p => p.Id == oldProvinceId)
                    .Select(p => p.Code)
                    .FirstOrDefaultAsync();

                oldProvinceCode = (oldProvinceCode ?? "").Trim().ToUpper();

                // Trường hợp bạn đổi district sang tỉnh khác thì shippingrate theo district cũ cũng nên cập nhật theo cách bạn muốn.
                // Ở đây: nếu chỉ đổi Code trong cùng tỉnh -> update DistrictCode
                // Nếu đổi cả tỉnh -> cập nhật ProvinceCode + DistrictCode theo tỉnh mới
                var newProvinceCode = await _context.Provinces
                    .Where(p => p.Id == model.ProvinceId)
                    .Select(p => p.Code)
                    .FirstOrDefaultAsync();

                newProvinceCode = (newProvinceCode ?? "").Trim().ToUpper();

                var rates = await _context.ShippingRates
                    .Where(r => r.ProvinceCode == oldProvinceCode && r.DistrictCode == oldCode)
                    .ToListAsync();

                foreach (var r in rates)
                {
                    r.ProvinceCode = newProvinceCode;
                    r.DistrictCode = model.Code;
                }
            }

            _context.Update(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { provinceId = model.ProvinceId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.Districts
                .Include(d => d.Province)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (item == null) return NotFound();

            var provinceCode = (item.Province?.Code ?? "").Trim().ToUpper();
            var districtCode = (item.Code ?? "").Trim().ToUpper();

            // Chặn xóa nếu còn ShippingRate dùng district này
            bool hasShippingRate = await _context.ShippingRates.AnyAsync(r =>
                r.ProvinceCode == provinceCode && r.DistrictCode == districtCode);

            if (hasShippingRate)
            {
                TempData["Error"] = "Không thể xóa Quận/Huyện vì còn cấu hình phí vận chuyển (ShippingRate) liên quan.";
                return RedirectToAction(nameof(Index), new { provinceId = item.ProvinceId });
            }

            _context.Districts.Remove(item);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { provinceId = item.ProvinceId });
        }
    }
}
