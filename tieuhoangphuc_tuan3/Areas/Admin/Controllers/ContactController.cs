using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Employer")]
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContactController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Admin/Contact
        public IActionResult Index(int page = 1, int pageSize = 10)
        {
            var total = _context.Contacts.Count();
            var contacts = _context.Contacts
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Total = total;
            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            return View(contacts);
        }

        // GET: Admin/Contact/Details/5
        public IActionResult Details(int id)
        {
            var contact = _context.Contacts.Find(id);
            if (contact == null) return NotFound();
            return View(contact);
        }

        // GET: Admin/Contact/Delete/5
        public IActionResult Delete(int id)
        {
            var contact = _context.Contacts.Find(id);
            if (contact == null) return NotFound();
            return View(contact);
        }

        // POST: Admin/Contact/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            var contact = _context.Contacts.Find(id);
            if (contact == null) return NotFound();

            _context.Contacts.Remove(contact);
            _context.SaveChanges();
            TempData["Success"] = "Đã xóa liên hệ thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}
