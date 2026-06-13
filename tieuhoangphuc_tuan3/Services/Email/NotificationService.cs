using System.Diagnostics;

namespace WebBanDienThoai.Services.Email // ✨ Khớp với thư mục Services/Email của bạn
{
    public class NotificationService
    {
        // Giả lập hệ thống tổng đài SMS
        public async Task SendSmsAsync(string phoneNumber, string message)
        {
            await Task.Delay(50);
            string logText = $"[SMS GATEWAY] Đang gửi tin nhắn đến SĐT {phoneNumber}: '{message}' -> TRẠNG THÁI: THÀNH CÔNG";
            Debug.WriteLine(logText);
            Console.WriteLine(logText);
        }

        // Giả lập hệ thống Push Notification ứng dụng di động
        public async Task SendPushNotificationAsync(string userId, string title, string body)
        {
            await Task.Delay(50);
            string logText = $"[FIREBASE PUSH] Gửi thông báo đến App User {userId}: 【{title}】 {body} -> TRẠNG THÁI: ĐÃ PHÁT";
            Debug.WriteLine(logText);
            Console.WriteLine(logText);
        }
    }
}