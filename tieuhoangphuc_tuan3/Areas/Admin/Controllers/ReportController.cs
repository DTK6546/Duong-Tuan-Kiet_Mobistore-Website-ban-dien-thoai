using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Employer")]
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 📊 Báo cáo tổng hợp: doanh thu + tồn kho
        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string type = "month")
        {
            // 🧩 Lọc các đơn hàng đã hoàn tất
            var orders = _context.Orders
                .Include(o => o.ApplicationUser)
                .Where(o => o.Status == OrderStatus.HoanTat);

            if (fromDate.HasValue)
                orders = orders.Where(o => o.OrderDate >= fromDate.Value);

            if (toDate.HasValue)
                orders = orders.Where(o => o.OrderDate <= toDate.Value);

            var orderList = await orders.ToListAsync();

            // 🧮 Tính toán doanh thu
            var totalRevenue = orderList.Sum(o => o.TotalPrice);
            var totalOrders = orderList.Count;

            // 🔢 Nhóm thống kê theo ngày / tháng / năm
            var revenueBy = type switch
            {
                "day" => orderList
                    .GroupBy(o => o.OrderDate.ToString("dd/MM/yyyy"))
                    .Select(g => new { Label = g.Key, Total = g.Sum(x => x.TotalPrice) }),
                "year" => orderList
                    .GroupBy(o => o.OrderDate.ToString("yyyy"))
                    .Select(g => new { Label = g.Key, Total = g.Sum(x => x.TotalPrice) }),
                _ => orderList
                    .GroupBy(o => o.OrderDate.ToString("MM/yyyy"))
                    .Select(g => new { Label = g.Key, Total = g.Sum(x => x.TotalPrice) })
            };

            // 🏷️ Thông tin tổng
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.RevenueBy = revenueBy.OrderBy(x => x.Label).ToList();
            ViewBag.Type = type;

            // ==============================
            // 📦 Thêm phần Báo cáo kho
            // ==============================
            var inventory = await _context.Products
                .Select(p => new
                {
                    p.Name,
                    p.Quantity,
                    p.MinStockLevel,
                    p.LastImportDate,
                    p.LastExportDate,
                    p.Price,
                    TotalValue = p.Quantity * p.Price
                })
                .OrderBy(p => p.Name)
                .ToListAsync();

            // Tổng giá trị hàng tồn kho
            ViewBag.TotalInventoryValue = inventory.Sum(i => i.TotalValue);
            ViewBag.Inventory = inventory;

            return View(orderList);
        }

        // 🧾 Xuất báo cáo kho ra Excel
        [HttpGet]
        public async Task<IActionResult> ExportInventoryToCsv()
        {
            var products = await _context.Products.ToListAsync();
            var csv = "Tên sản phẩm,Số lượng tồn,Ngưỡng cảnh báo,Giá,Giá trị tồn kho,Ngày nhập gần nhất,Ngày xuất gần nhất\n";

            foreach (var p in products)
            {
                csv += $"{p.Name},{p.Quantity},{p.MinStockLevel},{p.Price:N0},{(p.Quantity * p.Price):N0}," +
                       $"{p.LastImportDate?.ToString("dd/MM/yyyy")},{p.LastExportDate?.ToString("dd/MM/yyyy")}\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "BaoCaoTonKho.csv");
        }
    }
}
