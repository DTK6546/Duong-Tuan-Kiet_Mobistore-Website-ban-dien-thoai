using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Controllers
{
    public class ContactController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public ContactController(ApplicationDbContext context, IEmailSender emailSender)
        {
            _context = context;
            _emailSender = emailSender;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(Contact model)
        {
            if (ModelState.IsValid)
            {
                model.CreatedAt = DateTime.Now;
                _context.Contacts.Add(model);
                await _context.SaveChangesAsync();

                // Gửi email xác nhận cho khách
                var body = $@"
                <h3>Shop Điện Thoại đã nhận được liên hệ của bạn</h3>
                <p>Cảm ơn {model.Name} đã gửi thông tin liên hệ. Chúng tôi sẽ phản hồi trong thời gian sớm nhất!</p>
                <p><b>Nội dung:</b></p>
                <div style='white-space:pre-line'>{model.Message}</div>
                <hr><b>Shop Điện Thoại</b>
            ";
                await _emailSender.SendEmailAsync(model.Email, "Xác nhận liên hệ - Shop Điện Thoại", body);

                ViewBag.Success = "Gửi liên hệ thành công! Vui lòng kiểm tra email để xác nhận.";
                ModelState.Clear();
                return View();
            }
            return View(model);
        }
    }
}
