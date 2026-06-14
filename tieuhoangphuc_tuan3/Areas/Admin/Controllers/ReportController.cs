using System.Text;
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

        // 🧾 CHỨC NĂNG 10: XUẤT BÁO CÁO DOANH THU ĐƠN HÀNG (MỚI)
        [HttpGet]
        public async Task<IActionResult> ExportRevenueToCsv(DateTime? fromDate, DateTime? toDate)
        {
            var orders = _context.Orders
                .Include(o => o.ApplicationUser)
                .Where(o => o.Status == OrderStatus.HoanTat);

            if (fromDate.HasValue) orders = orders.Where(o => o.OrderDate >= fromDate.Value);
            if (toDate.HasValue) orders = orders.Where(o => o.OrderDate <= toDate.Value);

            var orderList = await orders.OrderByDescending(o => o.OrderDate).ToListAsync();

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Mã đơn hàng;Ngày đặt;Khách hàng;Tổng tiền (VNĐ);Trạng thái");

            foreach (var o in orderList)
            {
                csvBuilder.AppendLine($"{o.Id};{o.OrderDate:dd/MM/yyyy HH:mm};{o.ApplicationUser?.FullName ?? "Ẩn danh"};{o.TotalPrice};Hoàn tất");
            }

            // ✨ Chèn mã BOM mã hóa UTF-8 giúp Excel nhận diện dấu Tiếng Việt chuẩn 100%
            byte[] buffer = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            byte[] fileBytes = new byte[bom.Length + buffer.Length];
            Buffer.BlockCopy(bom, 0, fileBytes, 0, bom.Length);
            Buffer.BlockCopy(buffer, 0, fileBytes, bom.Length, buffer.Length);

            return File(fileBytes, "text/csv; charset=utf-8", $"BaoCaoDoanhThu_{DateTime.Now:yyyyMMdd}.csv");
        }

        // 🧾 CHỨC NĂNG 10: XUẤT BÁO CÁO KHO RA EXCEL (ĐÃ SỬA LỖI FONT & VỠ CỘT)
        [HttpGet]
        public async Task<IActionResult> ExportInventoryToCsv()
        {
            var products = await _context.Products.ToListAsync();

            var csvBuilder = new StringBuilder();
            // Dùng dấu chấm phẩy ; thay vì dấu phẩy để tránh lỗi chia nhầm cột khi tên sản phẩm chứa dấu ,
            csvBuilder.AppendLine("Tên sản phẩm;Số lượng tồn;Ngưỡng cảnh báo;Giá bán (VNĐ);Giá trị tồn kho (VNĐ);Ngày nhập gần nhất;Ngày xuất gần nhất");

            foreach (var p in products)
            {
                csvBuilder.AppendLine($"{p.Name};{p.Quantity};{p.MinStockLevel};{p.Price};{p.Quantity * p.Price};" +
                                       $"{p.LastImportDate?.ToString("dd/MM/yyyy")};{p.LastExportDate?.ToString("dd/MM/yyyy")}");
            }

            // ✨ Chèn mã BOM bảo vệ bảng mã hóa ký tự
            byte[] buffer = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            byte[] fileBytes = new byte[bom.Length + buffer.Length];
            Buffer.BlockCopy(bom, 0, fileBytes, 0, bom.Length);
            Buffer.BlockCopy(buffer, 0, fileBytes, bom.Length, buffer.Length);

            return File(fileBytes, "text/csv; charset=utf-8", $"BaoCaoTonKho_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}