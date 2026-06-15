using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Employer")]
    public class LoyaltyRewardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LoyaltyRewardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. DANH SÁCH PHẦN THƯỞNG GIẢI THƯỞNG
        public async Task<IActionResult> Index()
        {
            var rewards = await _context.LoyaltyRewards.ToListAsync();
            return View(rewards);
        }

        // 2. TẠO MỚI PHẦN THƯỞNG (GET)
        public IActionResult Create()
        {
            return View();
        }

        // 3. TẠO MỚI PHẦN THƯỞNG (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LoyaltyReward reward)
        {
            if (ModelState.IsValid)
            {
                _context.LoyaltyRewards.Add(reward);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Thêm phần thưởng tích điểm mới thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(reward);
        }

        // 4. CẬP NHẬT/SỬA PHẦN THƯỞNG (GET)
        public async Task<IActionResult> Edit(int id)
        {
            var reward = await _context.LoyaltyRewards.FindAsync(id);
            if (reward == null) return NotFound();
            return View(reward);
        }

        // 5. CẬP NHẬT/SỬA PHẦN THƯỞNG (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LoyaltyReward reward)
        {
            if (id != reward.Id) return NotFound();

            if (ModelState.IsValid)
            {
                _context.Update(reward);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Cập nhật thông tin phần thưởng thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(reward);
        }

        // 6. XÓA PHẦN THƯỞNG (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var reward = await _context.LoyaltyRewards.FindAsync(id);
            if (reward != null)
            {
                _context.LoyaltyRewards.Remove(reward);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã xóa bỏ phần thưởng thành công!";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}