using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        // GET: Admin/Store
        public async Task<IActionResult> Index()
        {
            var stores = await _context.Stores
                .OrderByDescending(s => s.IsActive)
                .ThenBy(s => s.Province)
                .ThenBy(s => s.District)
                .ThenBy(s => s.Name)
                .ToListAsync();

            return View(stores);
        }

        // GET: Admin/Store/Add
        public IActionResult Add()
        {
            return View();
        }

        // POST: Admin/Store/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(Store model)
        {
            if (!ModelState.IsValid)
            {
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
            if (store == null)
            {
                return NotFound();
            }

            return View(store);
        }

        // POST: Admin/Store/Update/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, Store model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
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
                {
                    return NotFound();
                }
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
            if (store == null)
            {
                return NotFound();
            }

            _context.Stores.Remove(store);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
