using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ShippingRateController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShippingRateController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===== helper: load danh sách tỉnh để đổ dropdown =====
        private async Task LoadProvinceSelectAsync(string? selectedProvinceCode = null)
        {
            var provinces = await _context.Provinces
                .OrderBy(p => p.Name)
                .Select(p => new { p.Code, p.Name })
                .ToListAsync();

            ViewBag.Provinces = provinces;
            ViewBag.SelectedProvinceCode = (selectedProvinceCode ?? "").Trim().ToUpper();
        }

        // ✅ API: lấy quận theo tỉnh (để dropdown District phụ thuộc Province)
        // GET: /Admin/ShippingRate/DistrictsByProvince?provinceCode=HCM
        [HttpGet]
        public async Task<IActionResult> DistrictsByProvince(string provinceCode)
        {
            if (string.IsNullOrWhiteSpace(provinceCode))
                return Json(new object[0]);

            var code = provinceCode.Trim().ToUpper();

            var list = await _context.Districts
                .Include(d => d.Province)
                .Where(d => d.Province!.Code == code)
                .OrderBy(d => d.Name)
                .Select(d => new { code = d.Code, name = d.Name })
                .ToListAsync();

            return Json(list);
        }

        // GET: /Admin/ShippingRate
        public async Task<IActionResult> Index(string? provinceCode)
        {
            var query = _context.ShippingRates.AsQueryable();

            if (!string.IsNullOrWhiteSpace(provinceCode))
            {
                var pc = provinceCode.Trim().ToUpper();
                query = query.Where(x => x.ProvinceCode == pc);
                ViewBag.ProvinceCode = pc;
            }
            else
            {
                ViewBag.ProvinceCode = "";
            }

            var items = await query
                .OrderBy(x => x.ProvinceCode)
                .ThenBy(x => x.DistrictCode)
                .ToListAsync();

            return View(items);
        }

        // GET: /Admin/ShippingRate/Create
        public async Task<IActionResult> Create()
        {
            await LoadProvinceSelectAsync();

            // Default giống web thật (bạn đổi tùy ý)
            return View(new ShippingRate
            {
                Fee = 30000,
                MinDays = 2,
                MaxDays = 4,
                ExpressFee = 50000,
                ExpressMinDays = 1,
                ExpressMaxDays = 2,
                FreeShipMinOrder = 5000000
            });
        }

        // POST: /Admin/ShippingRate/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ShippingRate model)
        {
            Normalize(model);

            if (!ModelState.IsValid)
            {
                await LoadProvinceSelectAsync(model.ProvinceCode);
                return View(model);
            }

            bool exists = await _context.ShippingRates.AnyAsync(x =>
                x.ProvinceCode == model.ProvinceCode &&
                x.DistrictCode == model.DistrictCode);

            if (exists)
            {
                await LoadProvinceSelectAsync(model.ProvinceCode);
                ModelState.AddModelError("", "Đã tồn tại cấu hình cho Tỉnh/TP và Quận/Huyện này.");
                return View(model);
            }

            _context.ShippingRates.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { provinceCode = model.ProvinceCode });
        }

        // GET: /Admin/ShippingRate/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.ShippingRates.FindAsync(id);
            if (item == null) return NotFound();

            await LoadProvinceSelectAsync(item.ProvinceCode);
            return View(item);
        }

        // POST: /Admin/ShippingRate/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ShippingRate model)
        {
            if (id != model.Id) return BadRequest();

            Normalize(model);

            if (!ModelState.IsValid)
            {
                await LoadProvinceSelectAsync(model.ProvinceCode);
                return View(model);
            }

            bool exists = await _context.ShippingRates.AnyAsync(x =>
                x.Id != model.Id &&
                x.ProvinceCode == model.ProvinceCode &&
                x.DistrictCode == model.DistrictCode);

            if (exists)
            {
                await LoadProvinceSelectAsync(model.ProvinceCode);
                ModelState.AddModelError("", "Đã tồn tại cấu hình cho Tỉnh/TP và Quận/Huyện này.");
                return View(model);
            }

            try
            {
                _context.Update(model);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                bool stillExists = await _context.ShippingRates.AnyAsync(x => x.Id == model.Id);
                if (!stillExists) return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index), new { provinceCode = model.ProvinceCode });
        }

        // POST: /Admin/ShippingRate/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.ShippingRates.FindAsync(id);
            if (item == null) return NotFound();

            var pc = item.ProvinceCode;

            _context.ShippingRates.Remove(item);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { provinceCode = pc });
        }

        private static void Normalize(ShippingRate model)
        {
            model.ProvinceCode = (model.ProvinceCode ?? "").Trim().ToUpper();

            model.DistrictCode = string.IsNullOrWhiteSpace(model.DistrictCode)
                ? null
                : model.DistrictCode.Trim().ToUpper();

            // Standard
            if (model.MinDays < 0) model.MinDays = 0;
            if (model.MaxDays < model.MinDays) model.MaxDays = model.MinDays;
            if (model.Fee < 0) model.Fee = 0;

            // Express
            if (model.ExpressMinDays < 0) model.ExpressMinDays = 0;
            if (model.ExpressMaxDays < model.ExpressMinDays) model.ExpressMaxDays = model.ExpressMinDays;
            if (model.ExpressFee < 0) model.ExpressFee = 0;

            // Free ship
            if (model.FreeShipMinOrder != null && model.FreeShipMinOrder < 0)
                model.FreeShipMinOrder = 0;
        }
    }
}
