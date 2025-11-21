using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class SubCategoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        public SubCategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // List
        public async Task<IActionResult> Index()
        {
            var subcategories = await _context.SubCategories.Include(s => s.Category).ToListAsync();
            return View(subcategories);
        }

        // Add (GET)
        public IActionResult Add()
        {
            ViewBag.Categories = _context.Categories.ToList();
            return View(new SubCategory());
        }

        // Add (POST)
        [HttpPost]
        public IActionResult Add(SubCategory subCategory)
        {
            if (ModelState.IsValid)
            {
                _context.SubCategories.Add(subCategory);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            var errors = ModelState.Values.SelectMany(v => v.Errors).ToList();
            ViewBag.Errors = errors;
            ViewBag.Categories = _context.Categories.ToList();
            return View(subCategory);
        }

        // Edit (GET)
        public IActionResult Update(int id)
        {
            var subCategory = _context.SubCategories.Find(id);
            if (subCategory == null) return NotFound();
            ViewBag.Categories = _context.Categories.ToList();
            return View(subCategory);
        }

        // Edit (POST)
        [HttpPost]
        public IActionResult Update(SubCategory subCategory)
        {
            if (ModelState.IsValid)
            {
                _context.SubCategories.Update(subCategory);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.Categories = _context.Categories.ToList();
            return View(subCategory);
        }

        // Delete
        [HttpPost]
        public IActionResult Delete(int id)
        {
            var subCategory = _context.SubCategories.Find(id);
            if (subCategory != null)
            {
                _context.SubCategories.Remove(subCategory);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }
    }
}
