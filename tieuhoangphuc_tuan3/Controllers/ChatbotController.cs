using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http; // Bổ sung để làm việc với Session
using WebBanDienThoai.Models;
using WebBanDienThoai.Services;

namespace WebBanDienThoai.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly GeminiService _geminiService;

        public ChatbotController(ApplicationDbContext db, GeminiService geminiService)
        {
            _db = db;
            _geminiService = geminiService;
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return Json(new { success = false, reply = "Nội dung tin nhắn trống." });
            }

            string lowerMsg = message.ToLower().Trim();

            // 🚀 BƯỚC ĐÁNH CHẶN 1: Kiểm tra xem phòng chat này trước đó đã chuyển giao cho nhân viên chưa
            bool isHumanActive = HttpContext.Session.GetString("IsHumanActive") == "true";

            // Nếu cờ đang bật -> AI im lặng hoàn toàn, trả về chuỗi trống để SignalR tự làm việc
            if (isHumanActive)
            {
                return Json(new { success = true, redirectToHuman = true, reply = "" });
            }

            // 🚀 BƯỚC ĐÁNH CHẶN 2: CHUYỂN HƯỚNG SANG NHÂN VIÊN HỖ TRỢ (LẦN ĐẦU TIÊN)
            if (lowerMsg.Contains("nhân viên") || lowerMsg.Contains("tư vấn viên") ||
                lowerMsg.Contains("gặp người") || lowerMsg.Contains("gặp nhân viên"))
            {
                // BẬT CÔNG TẮC: Lưu trạng thái vào Session để khóa AI lại ở các tin nhắn sau
                HttpContext.Session.SetString("IsHumanActive", "true");

                return Json(new
                {
                    success = true,
                    redirectToHuman = true,
                    reply = "🤖 Chuyển hướng thành công! Hệ thống đang kết nối bạn với chuyên viên hỗ trợ trực tiếp của MobiStore, vui lòng đợi..."
                });
            }

            // 3. TỐI ƯU HÓA RAG: Tìm kiếm từ khóa ngay tại Database (Lọc trực tiếp dưới SQL)
            var keywords = lowerMsg.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Xây dựng câu truy vấn động dựa trên các từ khóa của khách
            var query = _db.ProductFaqs.AsNoTracking().Where(f => f.IsActive);

            // Lấy tối đa 3 câu hỏi liên quan nhất
            var matchedFaqs = await query
                .Where(f => keywords.Any(k => f.Question.ToLower().Contains(k) || f.Answer.ToLower().Contains(k)))
                .Take(3)
                .Select(f => $"Câu hỏi: {f.Question}\nTrả lời: {f.Answer}")
                .ToListAsync();

            string contextFaq = matchedFaqs.Any() ? string.Join("\n\n", matchedFaqs) : string.Empty;

            // 4. GỌI SERVICE ĐÃ ĐƯỢC PHÂN TÁCH THAM SỐ CHUẨN (AI CHỈ CHẠY KHI CHƯA GỌI NHÂN VIÊN)
            string aiReply = await _geminiService.GetChatResponseAsync(message, contextFaq);

            return Json(new { success = true, redirectToHuman = false, reply = aiReply });
        }

        [HttpPost]
        public IActionResult EndHumanSupport()
        {
            HttpContext.Session.Remove("IsHumanActive");
            return Json(new { success = true });
        }
    }
}