using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebBanDienThoai.Controllers
{
    public class SecurityController : Controller
    {
        // Hàm gửi mã OTP giả lập qua SMS
        [HttpPost]
        public IActionResult SendSMSOTP(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber)) return Json(new { success = false, message = "Số điện thoại không hợp lệ." });

            // 1. Tạo mã ngẫu nhiên 6 chữ số
            string otpCode = new Random().Next(100000, 999999).ToString();

            // 2. Lưu vào Session để kiểm tra lại khi user nhập mã (hết hạn sau 5 phút)
            HttpContext.Session.SetString("SMS_OTP_CODE", otpCode);
            HttpContext.Session.SetString("SMS_OTP_PHONE", phoneNumber);

            // 3. ✨ GIẢ LẬP GỬI API: Bắn ra màn hình Output hệ thống
            System.Diagnostics.Debug.WriteLine($"==========================================");
            System.Diagnostics.Debug.WriteLine($"[MOBIRES SMS GATEWAY] Gửi thành công đến: {phoneNumber}");
            System.Diagnostics.Debug.WriteLine($"[MOBIRES SMS GATEWAY] Mã OTP 2FA của bạn là: {otpCode} (Hiệu lực 5 phút)");
            System.Diagnostics.Debug.WriteLine($"==========================================");

            return Json(new { success = true, message = "Mã OTP đã được gửi qua SMS (Hãy kiểm tra cửa sổ Output/Console Visual Studio)!" });
        }

        // Hàm xác nhận OTP do user gõ vào
        [HttpPost]
        public IActionResult VerifyOTP(string inputOtp)
        {
            string? savedOtp = HttpContext.Session.GetString("SMS_OTP_CODE");
            if (savedOtp != null && savedOtp == inputOtp)
            {
                HttpContext.Session.Remove("SMS_OTP_CODE"); // Xác thực xong thì xóa mã
                return Json(new { success = true, message = "Xác thực OTP 2FA thành công!" });
            }
            return Json(new { success = false, message = "Mã OTP không chính xác hoặc đã hết hạn." });
        }
    }
}