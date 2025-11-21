using Microsoft.AspNetCore.Mvc;
using WebBanDienThoai.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CategoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Index
        public async Task<IActionResult> Index()
        {
            // Get categories with their products count
            var categories = await _context.Categories
                .Include(c => c.Products)  // Eagerly load Products related to Category
                .ToListAsync();

            return View(categories); // Return categories with the Products list
        }


        // GET: Add
        public IActionResult Add() => View();

        // POST: Add
        [HttpPost]
        public IActionResult Add(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Add(category);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(category);
        }

        // GET: Update
        public IActionResult Update(int id)
        {
            var category = _context.Categories.Find(id);
            if (category == null) return NotFound();
            return View(category);
        }

        // POST: Update
        [HttpPost]
        public IActionResult Update(Category category)
        {
            if (ModelState.IsValid)
            {
                _context.Categories.Update(category);
                _context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View(category);
        }

        // POST: Delete
        [HttpPost]
        public IActionResult Delete(int id)
        {
            var category = _context.Categories.Include(c => c.Products).FirstOrDefault(c => c.Id == id);
            if (category != null)
            {
                // Gán lại CategoryId của các sản phẩm trong danh mục này về một danh mục mặc định (ví dụ danh mục với Id = 1)
                var defaultCategoryId = 16;

                foreach (var product in category.Products)
                {
                    product.CategoryId = defaultCategoryId; // Gán về CategoryId mặc định
                    _context.Products.Update(product);
                }

                // Xóa danh mục
                _context.Categories.Remove(category);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }




    }
}
