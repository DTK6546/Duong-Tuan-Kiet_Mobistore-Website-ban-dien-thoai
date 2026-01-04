using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class StoreController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StoreController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task LoadProvinceDistrictAsync(int? selectedProvinceId = null, int? selectedDistrictId = null)
        {
            var provinces = await _context.Provinces.OrderBy(p => p.Name).ToListAsync();
            ViewBag.Provinces = new SelectList(provinces, "Id", "Name", selectedProvinceId);

            var districts = new List<District>();
            if (selectedProvinceId.HasValue)
            {
                districts = await _context.Districts
                    .Where(d => d.ProvinceId == selectedProvinceId.Value)
                    .OrderBy(d => d.Name)
                    .ToListAsync();
            }

            ViewBag.Districts = new SelectList(districts, "Id", "Name", selectedDistrictId);
        }

        // ✅ API: lấy quận theo tỉnh (dropdown phụ thuộc)
        // GET: /Admin/Store/DistrictsByProvince?provinceId=1
        [HttpGet]
        public async Task<IActionResult> DistrictsByProvince(int provinceId)
        {
            var list = await _context.Districts
                .Where(d => d.ProvinceId == provinceId)
                .OrderBy(d => d.Name)
                .Select(d => new { id = d.Id, name = d.Name })
                .ToListAsync();

            return Json(list);
        }

        // GET: Admin/Store
        public async Task<IActionResult> Index()
        {
            var stores = await _context.Stores
                .Include(s => s.Province)
                .Include(s => s.District)
                .OrderByDescending(s => s.IsActive)
                .ThenBy(s => s.Province!.Name)
                .ThenBy(s => s.District!.Name)
                .ThenBy(s => s.Name)
                .ToListAsync();

            return View(stores);
        }

        // GET: Admin/Store/Add
        public async Task<IActionResult> Add()
        {
            await LoadProvinceDistrictAsync();
            return View(new Store());
        }

        // POST: Admin/Store/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Store model)
        {
            if (!ModelState.IsValid)
            {
                await LoadProvinceDistrictAsync(model.ProvinceId, model.DistrictId);
                return View(model);
            }

            // Validate District thuộc đúng Province
            bool districtOk = await _context.Districts.AnyAsync(d => d.Id == model.DistrictId && d.ProvinceId == model.ProvinceId);
            if (!districtOk)
            {
                ModelState.AddModelError("", "Quận/Huyện không thuộc Tỉnh/TP đã chọn.");
                await LoadProvinceDistrictAsync(model.ProvinceId, model.DistrictId);
                return View(model);
            }

            _context.Stores.Add(model);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Store/Update/5
        public async Task<IActionResult> Update(int id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store == null) return NotFound();

            await LoadProvinceDistrictAsync(store.ProvinceId, store.DistrictId);
            return View(store);
        }

        // POST: Admin/Store/Update/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, Store model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadProvinceDistrictAsync(model.ProvinceId, model.DistrictId);
                return View(model);
            }

            bool districtOk = await _context.Districts.AnyAsync(d => d.Id == model.DistrictId && d.ProvinceId == model.ProvinceId);
            if (!districtOk)
            {
                ModelState.AddModelError("", "Quận/Huyện không thuộc Tỉnh/TP đã chọn.");
                await LoadProvinceDistrictAsync(model.ProvinceId, model.DistrictId);
                return View(model);
            }

            try
            {
                _context.Stores.Update(model);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Stores.AnyAsync(s => s.Id == id))
                    return NotFound();
                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Admin/Store/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var store = await _context.Stores.FindAsync(id);
            if (store == null) return NotFound();

            _context.Stores.Remove(store);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
