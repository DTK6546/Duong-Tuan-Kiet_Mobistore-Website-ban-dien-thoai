using Microsoft.AspNetCore.Mvc;

namespace WebBanDienThoai.Controllers
{
    public class AboutController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
