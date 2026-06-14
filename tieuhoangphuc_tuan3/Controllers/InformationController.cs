using Microsoft.AspNetCore.Mvc;

namespace WebBanDienThoai.Controllers
{
    public class InformationController : Controller
    {
        // Chức năng 1: Giới thiệu về cửa hàng (About Us)
        public IActionResult AboutUs()
        {
            return View();
        }

        // Chức năng 2: Điều khoản sử dụng & Chính sách bảo mật
        public IActionResult PrivacyPolicy()
        {
            return View();
        }

        // Chức năng 3: FAQ Trang chủ (Câu hỏi thường gặp chung)
        public IActionResult FAQ()
        {
            return View();
        }
    }
}