using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Controllers
{
    public class NewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public NewsController(ApplicationDbContext context) => _context = context;

        // GET: /News
        public async Task<IActionResult> Index()
        {
            var news = await _context.News.OrderByDescending(n => n.CreatedAt).ToListAsync();
            return View(news);
        }

        // GET: /News/Detail/5
        public async Task<IActionResult> Detail(int id)
        {
            var news = await _context.News.FirstOrDefaultAsync(n => n.Id == id);
            if (news == null) return NotFound();
            return View(news);
        }
    }
}
