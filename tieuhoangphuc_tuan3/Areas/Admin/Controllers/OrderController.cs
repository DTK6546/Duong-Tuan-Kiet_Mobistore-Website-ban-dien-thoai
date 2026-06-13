using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;
using Microsoft.AspNetCore.Identity.UI.Services; // Nạp dịch vụ IEmailSender chuẩn của bạn
using WebBanDienThoai.Services.Email; // Nạp đường dẫn dịch vụ thông báo mới tạo

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _emailSender; // ✨ Khai báo interface email có sẵn
        private readonly NotificationService _notifService; // ✨ Khai báo dịch vụ SMS/Push mới

        // Cập nhật Constructor để nạp (Inject) các dịch vụ thông báo đa kênh
        public OrderController(
            ApplicationDbContext db,
            IEmailSender emailSender,
            NotificationService notifService)
        {
            _db = db;
            _emailSender = emailSender;
            _notifService = notifService;
        }

        public IActionResult Index(string search, int? status)
        {
            var orders = _db.Orders
                .Include(o => o.ApplicationUser)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                orders = orders.Where(o => o.ApplicationUser.FullName.Contains(search) || o.Id.ToString() == search);

            if (status != null)
                orders = orders.Where(o => (int)o.Status == status.Value);

            return View(orders.ToList());
        }

        public IActionResult Details(int id)
        {
            var order = _db.Orders
                .Include(o => o.ApplicationUser)
                .Include(o => o.Store)
                .Include(o => o.Shipper)
                .Include(o => o.OrderLogs)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
                return NotFound();

            ViewBag.Shippers = _db.Shippers.Where(s => s.IsActive).ToList();
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
        {
            var order = _db.Orders
                .Include(o => o.OrderDetails)
                .Include(o => o.OrderLogs)
                .FirstOrDefault(o => o.Id == id);

            if (order == null) return NotFound();

            order.Status = status;

            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();

            string description = status switch
            {
                OrderStatus.ChoXacNhan => "Đơn hàng đã được khởi tạo thành công và chờ tổng đài xác nhận.",
                OrderStatus.DangXuLy => "Đơn hàng đã được xác nhận. Nhân viên đang kiểm kho và đóng gói.",
                OrderStatus.DangGiao => "Bưu tá đã lấy hàng thành công và bưu kiện đang trên đường vận chuyển.",
                OrderStatus.DaGiao => "Gói hàng đã được giao đến bưu cục phát. Shipper chuẩn bị phát hàng.",
                OrderStatus.HoanTat => "Người mua xác nhận đã nhận hàng thành công. Đơn hàng hoàn tất.",
                OrderStatus.DaHuy => "Đơn hàng đã bị hủy bỏ trên hệ thống.",
                OrderStatus.TraHang => "Hệ thống tiếp nhận yêu cầu trả hàng / hoàn tiền từ người mua.",
                _ => "Trạng thái đơn hàng có sự thay đổi."
            };

            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                StatusDescription = description,
                LogDate = DateTime.Now,
                Location = "Hệ thống MobiStore"
            });

            if (status == OrderStatus.HoanTat || status == OrderStatus.TraHang)
            {
                foreach (var item in order.OrderDetails)
                {
                    int delta = status == OrderStatus.HoanTat ? -item.Quantity : item.Quantity;
                    ProductVariant variant = null;
                    if (item.VariantId.HasValue)
                    {
                        variant = _db.ProductVariants.FirstOrDefault(v => v.Id == item.VariantId.Value);
                    }
                    var product = _db.Products.FirstOrDefault(p => p.Id == item.ProductId);
                    if (variant != null) { variant.Stock += delta; }
                    if (product != null)
                    {
                        product.Quantity += delta;
                        if (status == OrderStatus.HoanTat) { product.LastExportDate = DateTime.Now; }
                    }
                }

                if (status == OrderStatus.HoanTat)
                {
                    var user = _db.ApplicationUsers.FirstOrDefault(u => u.Id == order.UserId);
                    if (user != null)
                    {
                        int pointsEarned = (int)(order.TotalPrice / 100000);
                        user.CurrentPoints += pointsEarned;
                        user.RankingPoints += pointsEarned;

                        if (!string.IsNullOrEmpty(user.Email) && order.OrderDetails != null && order.OrderDetails.Any())
                        {
                            var firstItem = order.OrderDetails.First();
                            var prodInfo = _db.Products.FirstOrDefault(p => p.Id == firstItem.ProductId);

                            if (prodInfo != null)
                            {
                                string productUrl = $"{Request.Scheme}://{Request.Host}/Product/Display/{prodInfo.Id}";
                                string requestEmailSubject = $"[MobiStore] Mời bạn đánh giá sản phẩm {prodInfo.Name}";
                                string requestEmailBody = $@"
                                    <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; border: 1px solid #eee; padding: 20px; border-radius: 8px;'>
                                        <h2 style='color: #059669; text-align: center;'>Cảm ơn bạn đã tin tưởng mua sắm tại MobiStore!</h2>
                                        <p>Xin chào <strong>{user.FullName}</strong>,</p>
                                        <p>Đơn hàng <strong>#{order.Id}</strong> của bạn đã giao dịch thành công. Ý kiến của bạn là điều vô cùng quý giá để chúng tôi nâng cao chất lượng dịch vụ.</p>
                                        <p>MobiStore trân trọng mời bạn chia sẻ cảm nhận thực tế về thiết bị <strong>{prodInfo.Name}</strong> mà bạn vừa sở hữu.</p>
                                        <div style='text-align: center; margin: 30px 0;'>
                                            <a href='{productUrl}#review-section' style='background-color: #059669; color: white; padding: 12px 25px; text-decoration: none; border-radius: 25px; font-weight: bold; display: inline-block; box-shadow: 0 4px 6px rgba(0,0,0,0.1);'>✍️ Viết Đánh Giá Để Lấy Tích Xanh Ngay</a>
                                        </div>
                                        <p style='font-size: 13px; color: #666; font-style: italic; text-align: center;'>Bình luận của bạn sẽ được hiển thị kèm Huy hiệu xác minh đã mua hàng độc lập.</p>
                                        <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                                        <p style='font-size: 11px; color: #777; text-align: center;'>MobiStore - Uy tín kiến tạo niềm tin.</p>
                                    </div>";

                                // Gọi luồng gửi Mail nền bất đồng bộ
                                _ = Task.Run(() => _emailSender.SendEmailAsync(user.Email, requestEmailSubject, requestEmailBody));
                            }
                        }
                    }
                }
            }

            // =========================================================================
            // ✨ CHỨC NĂNG 6: TỰ ĐỘNG PHÁT THÔNG BÁO ĐA KÊNH KHI CẬP NHẬT TRẠNG THÁI
            // =========================================================================
            try
            {
                var customer = _db.ApplicationUsers.FirstOrDefault(u => u.Id == order.UserId);
                if (customer != null)
                {
                    string orderUrl = $"{Request.Scheme}://{Request.Host}/Order/Details/{order.Id}";

                    // 📬 1. GỬI TIN NHẮN SMS GIẢ LẬP (LOG RA CONSOLE)
                    string smsMessage = $"MobiStore: Don hang #{order.Id} cua ban da chuyen sang trang thai: {description}. Xem tai {orderUrl}";
                    await _notifService.SendSmsAsync(customer.PhoneNumber, smsMessage);

                    // 📲 2. GỬI PUSH NOTIFICATION APP GIẢ LẬP (LOG RA CONSOLE)
                    string pushTitle = $"Cập nhật đơn hàng #{order.Id}";
                    string pushBody = $"Đơn hàng của bạn hiện tại: {description}. Bấm để theo dõi hành trình di chuyển bưu kiện.";
                    await _notifService.SendPushNotificationAsync(customer.Id, pushTitle, pushBody);

                    // 📧 3. GỬI EMAIL THÔNG BÁO THỰC TẾ QUA DỊCH VỤ CỦA BẠN
                    if (!string.IsNullOrEmpty(customer.Email))
                    {
                        string emailSubject = $"[MobiStore] Cập nhật trạng thái đơn hàng #{order.Id}";
                        string emailBody = $@"
                            <div style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; border: 1px solid #eee; padding: 20px; border-radius: 8px;'>
                                <h2 style='color: #dc3545; text-align: center;'>MobiStore - Cập nhật hành trình bưu kiện</h2>
                                <p>Xin chào <strong>{customer.FullName}</strong>,</p>
                                <p>Hệ thống MobiStore xin thông báo đơn hàng <strong>#{order.Id}</strong> của bạn đã có cập nhật mới:</p>
                                <div style='background-color: #f8f9fa; border-left: 4px solid #dc3545; padding: 15px; margin: 20px 0; font-weight: bold;'>
                                    Trạng thái: {description}
                                </div>
                                <p>Lộ trình bưu kiện đang được bưu tá cập nhật liên tục trên trang quản trị cá nhân hành trình Real-time.</p>
                                <div style='text-align: center; margin-top: 30px;'>
                                    <a href='{orderUrl}' style='background-color: #dc3545; color: white; padding: 12px 25px; text-decoration: none; border-radius: 25px; font-weight: bold; display: inline-block;'>Xem Chi Tiết Đơn Hàng Tại Đây</a>
                                </div>
                                <hr style='border: none; border-top: 1px solid #eee; margin: 30px 0;' />
                                <p style='font-size: 11px; color: #777; text-align: center;'>Đây là email tự động từ hệ thống MobiStore, vui lòng không phản hồi lại email này.</p>
                            </div>";

                        // Chạy nền tác vụ gửi Email thực tế để tránh làm chậm luồng phản hồi trang Admin
                        _ = Task.Run(() => _emailSender.SendEmailAsync(customer.Email, emailSubject, emailBody));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi gửi thông báo đa kênh: {ex.Message}");
            }
            // =========================================================================

            _db.SaveChanges();
            TempData["SuccessMessage"] = "Trạng thái đơn hàng và nhật ký hành trình đã được cập nhật!";
            return RedirectToAction("Details", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateLogistics(int orderId, int shipperId, string trackingNumber, string initialLogDescription)
        {
            var order = _db.Orders
                .Include(o => o.OrderLogs)
                .FirstOrDefault(o => o.Id == orderId);

            if (order == null) return NotFound();

            order.ShipperId = shipperId;
            order.TrackingNumber = (trackingNumber ?? "").Trim();

            if (order.Status == OrderStatus.ChoXacNhan)
            {
                order.Status = OrderStatus.DangXuLy;
            }

            if (order.OrderLogs == null) order.OrderLogs = new List<OrderLog>();

            order.OrderLogs.Add(new OrderLog
            {
                OrderId = order.Id,
                StatusDescription = !string.IsNullOrWhiteSpace(initialLogDescription)
                    ? initialLogDescription.Trim()
                    : "Đơn hàng đã được bàn giao thành công cho nhân viên giao vận nội bộ.",
                LogDate = DateTime.Now,
                Location = "Kho tổng MobiStore"
            });

            _db.SaveChanges();

            TempData["SuccessMessage"] = "Đã phân bổ Shipper phụ trách và phát hành mã vận đơn thành công!";
            return RedirectToAction("Details", new { id = orderId });
        }

        public IActionResult CreateShipper()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateShipper(Shipper shipper)
        {
            if (ModelState.IsValid)
            {
                shipper.IsActive = true;
                _db.Shippers.Add(shipper);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã thêm shipper {shipper.FullName} thành công!";
                return RedirectToAction("Index");
            }
            return View(shipper);
        }
    }
}