using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;
using WebBanDienThoai.Services.SignalR; // để dùng ChatHub

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Employer")]
    public class NewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<ChatHub> _chatHub;

        public NewsController(ApplicationDbContext context, IHubContext<ChatHub> chatHub)
        {
            _context = context;
            _chatHub = chatHub;
        }

        // GET: Admin/News
        public async Task<IActionResult> Index()
        {
            var news = await _context.News
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
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
        public async Task<IActionResult> Create(News model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.News.Add(model);
                await _context.SaveChangesAsync();

                var url = Url.Action("Detail", "News", new { area = "", id = model.Id }, Request.Scheme);

                var summary = !string.IsNullOrEmpty(model.Summary)
                    ? model.Summary
                    : (model.Content?.Length > 120 ? model.Content.Substring(0, 120) + "..." : model.Content);

                await _chatHub.Clients.All.SendAsync(
                    "ReceiveNewsNotification",
                    model.Title,                                   // 1: title
                    summary ?? string.Empty,                      // 2: summary
                    url,                                          // 3: url
                    model.CreatedAt.ToString("dd/MM/yyyy HH:mm")  // 4: time
                );

                TempData["Success"] = "Thêm tin tức thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Admin/News/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null) return NotFound();
            return View(news);
        }

        // POST: Admin/News/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(News model)
        {
            if (ModelState.IsValid)
            {
                var existing = await _context.News.FindAsync(model.Id);
                if (existing == null) return NotFound();

                // Cập nhật field, giữ nguyên CreatedAt
                existing.Title = model.Title;
                existing.Summary = model.Summary;
                existing.Content = model.Content;
                existing.ImageUrl = model.ImageUrl;
                existing.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                var url = Url.Action("Detail", "News", new { area = "", id = existing.Id }, Request.Scheme);

                var summary = !string.IsNullOrEmpty(existing.Summary)
                    ? existing.Summary
                    : (existing.Content?.Length > 120 ? existing.Content.Substring(0, 120) + "..." : existing.Content);

                await _chatHub.Clients.All.SendAsync(
                    "ReceiveNewsNotification",
                    existing.Title,                                           // 1: title
                    summary ?? string.Empty,                                 // 2: summary
                    url,                                                     // 3: url
                    existing.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? ""   // 4: time
                );

                TempData["Success"] = "Cập nhật tin tức thành công!";
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

            var title = news.Title;

            _context.News.Remove(news);
            await _context.SaveChangesAsync();

            await _chatHub.Clients.All.SendAsync(
                "ReceiveNewsNotification",
                title,                                                 // 1: title
                "Tin tức đã bị xóa khỏi hệ thống.",                   // 2: summary
                null,                                                  // 3: url (không có)
                DateTime.Now.ToString("dd/MM/yyyy HH:mm")             // 4: time
            );

            TempData["Success"] = "Đã xóa tin tức!";
            return RedirectToAction(nameof(Index));
        }
    }
}
