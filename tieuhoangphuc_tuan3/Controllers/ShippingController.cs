using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebBanDienThoai.Controllers
{
    [Route("Shipping")]
    public class ShippingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShippingController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Shipping/Provinces
        [HttpGet("Provinces")]
        public async Task<IActionResult> Provinces()
        {
            var list = await _context.Provinces
                .OrderBy(p => p.Name)
                .Select(p => new { code = p.Code, name = p.Name })
                .ToListAsync();

            return Json(list);
        }

        // GET: /Shipping/Districts?provinceCode=HCM
        [HttpGet("Districts")]
        public async Task<IActionResult> Districts(string provinceCode)
        {
            if (string.IsNullOrWhiteSpace(provinceCode))
                return Json(new object[0]);

            var code = provinceCode.Trim().ToUpper();

            var list = await _context.Districts
                .Include(d => d.Province)
                .Where(d => d.Province!.Code == code)
                .OrderBy(d => d.Name)
                .Select(d => new { code = d.Code, name = d.Name })
                .ToListAsync();

            return Json(list);
        }

        // GET: /Shipping/Quote?provinceCode=HCM&districtCode=Q1&method=standard&orderValue=32000000
        [HttpGet("Quote")]
        public async Task<IActionResult> Quote(string provinceCode, string? districtCode, string method = "standard", decimal? orderValue = null)
        {
            if (string.IsNullOrWhiteSpace(provinceCode))
                return BadRequest(new { ok = false, error = "Missing provinceCode" });

            provinceCode = provinceCode.Trim().ToUpper();
            districtCode = string.IsNullOrWhiteSpace(districtCode) ? null : districtCode.Trim().ToUpper();
            method = (method ?? "standard").Trim().ToLower();

            var rate = await _context.ShippingRates
                .AsNoTracking()
                .Where(x => x.ProvinceCode == provinceCode && (x.DistrictCode == districtCode || x.DistrictCode == null))
                .OrderByDescending(x => x.DistrictCode != null) // ưu tiên theo quận
                .FirstOrDefaultAsync();

            if (rate == null)
                return Json(new { ok = false, message = "Chưa cấu hình phí vận chuyển cho khu vực này." });

            bool isExpress = method == "express" || method == "nhanh";
            decimal fee = isExpress ? rate.ExpressFee : rate.Fee;
            int minDays = isExpress ? rate.ExpressMinDays : rate.MinDays;
            int maxDays = isExpress ? rate.ExpressMaxDays : rate.MaxDays;

            // Free ship theo giá trị đơn
            bool freeApplied = false;
            if (rate.FreeShipMinOrder != null && orderValue != null && orderValue.Value >= rate.FreeShipMinOrder.Value)
            {
                fee = 0;
                freeApplied = true;
            }

            // ETA theo "ngày làm việc" (bỏ CN)
            var from = AddBusinessDays(DateTime.Today, minDays);
            var to = AddBusinessDays(DateTime.Today, maxDays);

            return Json(new
            {
                ok = true,
                method = isExpress ? "express" : "standard",
                fee,
                feeText = fee == 0 ? "Miễn phí" : $"{fee:N0} đ",
                minDays,
                maxDays,
                etaFrom = from.ToString("dd/MM/yyyy"),
                etaTo = to.ToString("dd/MM/yyyy"),
                etaText = $"Dự kiến {from:dd/MM} - {to:dd/MM}",
                freeApplied,
                freeShipMinOrder = rate.FreeShipMinOrder
            });
        }

        // helper: cộng N "ngày làm việc" (bỏ Chủ nhật)
        private static DateTime AddBusinessDays(DateTime start, int days)
        {
            var d = start;
            int added = 0;
            while (added < days)
            {
                d = d.AddDays(1);
                if (d.DayOfWeek != DayOfWeek.Sunday)
                    added++;
            }
            return d;
        }
    }
}
