using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Employer.Controllers
{
    [Area("Employer")]
    [Authorize(Roles = "Admin,Employer")]
    public class NewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public NewsController(ApplicationDbContext context) => _context = context;

        // GET: Admin/News
        public async Task<IActionResult> Index()
        {
            var news = await _context.News.OrderByDescending(n => n.CreatedAt).ToListAsync();
            return View(news);
        }

        // GET: Admin/News/Create
        public IActionResult Create()
        {
            return View(new News());
        }

        // POST: Admin/News/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(News model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.News.Add(model);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Admin/News/Edit/5
        public IActionResult Edit(int id)
        {
            var news = _context.News.Find(id);
            if (news == null) return NotFound();
            return View(news);
        }

        // POST: Admin/News/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(News model)
        {
            if (ModelState.IsValid)
            {
                _context.News.Update(model);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Admin/News/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var news = await _context.News.FindAsync(id);
            if (news == null) return NotFound();
            return View(news);
        }

        // POST: Admin/News/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null) return NotFound();
            _context.News.Remove(news);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Đã xóa tin tức!";
            return RedirectToAction(nameof(Index));
        }
    }
}
