using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        // 📊 Báo cáo tổng hợp cũ: doanh thu + tồn kho (GIỮ NGUYÊN VẸN)
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

        // 📈 CHỨC NĂNG MỚI: DASHBOARD ANALYTICS CHUYÊN SÂU BI ĐẲNG CẤP
        public async Task<IActionResult> Dashboard(string timeline = "month")
        {
            // 1. Tải toàn bộ đơn hàng hoàn tất kèm chi tiết sản phẩm và danh mục để tính toán
            var completedOrders = await _context.Orders
                .Include(o => o.OrderDetails).ThenInclude(d => d.Product).ThenInclude(p => p.Category)
                .Where(o => o.Status == OrderStatus.HoanTat)
                .ToListAsync();

            // 💵 KPIs Thẻ Tài Chính
            ViewBag.TotalRevenue = completedOrders.Sum(o => o.TotalPrice);
            ViewBag.TotalOrdersCount = completedOrders.Count;

            // 📦 Tính toán tỷ lệ chuyển đổi (Conversion Rate)
            var allOrdersCount = await _context.Orders.CountAsync();
            ViewBag.ConversionRate = allOrdersCount > 0
                ? ((double)completedOrders.Count / allOrdersCount * 100).ToString("F1")
                : "0.0";

            // 👥 Phân tích khách hàng (Mới vs Quay lại mua hàng)
            var allUsersCount = await _context.Users.CountAsync();
            var recurringCustomerIds = completedOrders
                .GroupBy(o => o.UserId)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .ToList();

            ViewBag.RecurringCustomers = recurringCustomerIds.Count;
            ViewBag.NewCustomers = Math.Max(0, allUsersCount - recurringCustomerIds.Count);

            // 💰 Tính toán doanh thu theo Danh mục sản phẩm (Categories) phục vụ biểu đồ Tròn
            var categoryData = completedOrders
                .SelectMany(o => o.OrderDetails)
                .GroupBy(d => d.Product?.Category?.Name ?? "Khác")
                .Select(g => new { CategoryName = g.Key, Amount = g.Sum(x => x.Quantity * x.Price) })
                .ToList();

            ViewBag.CategoryLabels = string.Join(",", categoryData.Select(c => $"\"{c.CategoryName}\""));
            ViewBag.CategoryValues = string.Join(",", categoryData.Select(c => c.Amount));

            // ⭐ Thống kê TOP 5 sản phẩm bán chạy nhất hệ thống
            var topProducts = completedOrders
                .SelectMany(o => o.OrderDetails)
                .GroupBy(d => new { d.ProductId, d.Product?.Name })
                .Select(g => new {
                    ProductName = g.Key.Name ?? "Sản phẩm đã ẩn",
                    TotalSold = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Quantity * x.Price)
                })
                .OrderByDescending(p => p.TotalSold)
                .Take(5)
                .ToList();
            ViewBag.TopProducts = topProducts;

            // 📈 Xử lý nhóm thời gian cho biểu đồ đường xu hướng doanh thu (Main Chart)
            var revenueTrend = timeline switch
            {
                "day" => completedOrders
                    .GroupBy(o => o.OrderDate.ToString("dd/MM"))
                    .Select(g => new { Label = g.Key, Total = g.Sum(x => x.TotalPrice) }),
                "year" => completedOrders
                    .GroupBy(o => o.OrderDate.ToString("yyyy"))
                    .Select(g => new { Label = g.Key, Total = g.Sum(x => x.TotalPrice) }),
                _ => completedOrders
                    .GroupBy(o => o.OrderDate.ToString("MM/yyyy"))
                    .Select(g => new { Label = g.Key, Total = g.Sum(x => x.TotalPrice) })
            };

            var trendList = revenueTrend.OrderBy(x => x.Label).ToList();
            ViewBag.ChartLabels = string.Join(",", trendList.Select(x => $"\"{x.Label}\""));
            ViewBag.ChartData = string.Join(",", trendList.Select(x => x.Total));
            ViewBag.Timeline = timeline;

            return View();
        }

        // 📝 XEM LỊCH SỬ NHẬT KÝ KHO HÀNG (STOCK HISTORY LOGS)
        public async Task<IActionResult> InventoryLogs()
        {
            var logs = await _context.InventoryLogs
                .Include(l => l.Product)
                .OrderByDescending(l => l.CreatedAt)
                .Take(100) // Lấy 100 nhật ký gần nhất để tránh lag trang
                .ToListAsync();

            return View(logs);
        }

        // 🧾 XUẤT BÁO CÁO DOANH THU ĐƠN HÀNG (GIỮ NGUYÊN VẸN CỦA BẠN - ĐÃ THÊM LỆNH TỰ TÁCH CỘT)
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
            csvBuilder.AppendLine("sep=;");
            csvBuilder.AppendLine("Mã đơn hàng;Ngày đặt;Khách hàng;Tổng tiền (VNĐ);Trạng thái");

            foreach (var o in orderList)
            {
                csvBuilder.AppendLine($"{o.Id};{o.OrderDate:dd/MM/yyyy HH:mm};{o.ApplicationUser?.FullName ?? "Ẩn danh"};{o.TotalPrice};Hoàn tất");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            byte[] fileBytes = new byte[bom.Length + buffer.Length];
            Buffer.BlockCopy(bom, 0, fileBytes, 0, bom.Length);
            Buffer.BlockCopy(buffer, 0, fileBytes, bom.Length, buffer.Length);

            return File(fileBytes, "text/csv; charset=utf-8", $"BaoCaoDoanhThu_{DateTime.Now:yyyyMMdd}.csv");
        }

        // 🧾 XUẤT BÁO CÁO KHO RA EXCEL (GIỮ NGUYÊN VẸN CỦA BẠN - ĐÃ THÊM LỆNH TỰ TÁCH CỘT)
        [HttpGet]
        public async Task<IActionResult> ExportInventoryToCsv()
        {
            var products = await _context.Products.ToListAsync();

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("sep=;");
            csvBuilder.AppendLine("Tên sản phẩm;Số lượng tồn;Ngưỡng cảnh báo;Giá bán (VNĐ);Giá trị tồn kho (VNĐ);Ngày nhập gần nhất;Ngày xuất gần nhất");

            foreach (var p in products)
            {
                csvBuilder.AppendLine($"{p.Name};{p.Quantity};{p.MinStockLevel};{p.Price};{p.Quantity * p.Price};" +
                                       $"{p.LastImportDate?.ToString("dd/MM/yyyy")};{p.LastExportDate?.ToString("dd/MM/yyyy")}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            byte[] fileBytes = new byte[bom.Length + buffer.Length];
            Buffer.BlockCopy(bom, 0, fileBytes, 0, bom.Length);
            Buffer.BlockCopy(buffer, 0, fileBytes, bom.Length, buffer.Length);

            return File(fileBytes, "text/csv; charset=utf-8", $"BaoCaoTonKho_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}