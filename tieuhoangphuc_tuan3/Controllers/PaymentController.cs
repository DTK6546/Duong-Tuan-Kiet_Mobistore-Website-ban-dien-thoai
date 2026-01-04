using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Globalization;
using WebBanDienThoai.Extensions;
using WebBanDienThoai.Models;
using WebBanDienThoai.Services;
using WebBanDienThoai.Services.Momo;
using WebBanDienThoai.Services.VNPay;

namespace WebBanDienThoai.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IMomoService _momoService;
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<PayPalSettings> _paypalSettings;
        private readonly ApplicationDbContext _dbContext;

        public PaymentController(
            IMomoService momoService,
            IConfiguration config,
            UserManager<ApplicationUser> userManager,
            IOptions<PayPalSettings> paypalSettings,
            ApplicationDbContext dbContext)
        {
            _momoService = momoService;
            _config = config;
            _userManager = userManager;
            _paypalSettings = paypalSettings;
            _dbContext = dbContext;
        }

        // ==================================================================================
        // 🛠️ SESSION HELPERS
        // ==================================================================================

        // ✅ UPDATED: thêm shipMethod
        private void SaveSessionInfo(
            string? deliveryMethod,
            int? storeId,
            string? provinceCode,
            string? districtCode,
            decimal? shippingFee,
            string? shipMethod)
        {
            HttpContext.Session.SetString("DeliveryMethod",
                string.IsNullOrWhiteSpace(deliveryMethod) ? "ShipToHome" : deliveryMethod);

            if (storeId.HasValue) HttpContext.Session.SetInt32("StoreId", storeId.Value);
            else HttpContext.Session.Remove("StoreId");

            HttpContext.Session.SetString("ProvinceCode", provinceCode ?? "");
            HttpContext.Session.SetString("DistrictCode", districtCode ?? "");

            // Lưu theo invariant để parse không dính dấu phẩy/chấm
            HttpContext.Session.SetString("ShippingFee",
                (shippingFee ?? 0m).ToString(CultureInfo.InvariantCulture));

            // shipMethod: standard/express
            HttpContext.Session.SetString("ShipMethod",
                string.IsNullOrWhiteSpace(shipMethod) ? "standard" : shipMethod.Trim().ToLower());
        }

        // ✅ UPDATED: trả thêm shipMethod
        private (decimal total, decimal ship, string prov, string dist, decimal subtotal, decimal vat, decimal afterDiscount, string shipMethod)
            GetCalculatedTotals(ShoppingCart cart)
        {
            decimal subtotal = cart.Items.Sum(i =>
                (i.Price + (i.Warranties?.Sum(w => w.Price) ?? 0m)) * i.Quantity);

            decimal discount = cart.DiscountAmount;
            if (discount < 0) discount = 0;

            decimal afterDiscount = Math.Max(0m, subtotal - discount);
            decimal vatAmount = Math.Round(afterDiscount * 0.10m, 0);

            var shipStr = HttpContext.Session.GetString("ShippingFee") ?? "0";
            decimal ship = 0m;
            decimal.TryParse(shipStr, NumberStyles.Any, CultureInfo.InvariantCulture, out ship);

            string prov = HttpContext.Session.GetString("ProvinceCode") ?? "";
            string dist = HttpContext.Session.GetString("DistrictCode") ?? "";
            string shipMethod = HttpContext.Session.GetString("ShipMethod") ?? "standard";

            decimal total = afterDiscount + vatAmount + ship;
            return (total, ship, prov, dist, subtotal, vatAmount, afterDiscount, shipMethod);
        }

        // ==================================================================================
        // ✅ TẠO ORDER (giảm SaveChanges, fill đủ field)
        // ==================================================================================

        private async Task<Order> CreateAndSaveOrder(ShoppingCart cart, string paymentMethodName)
        {
            var user = User.Identity.IsAuthenticated ? await _userManager.GetUserAsync(User) : null;
            var totals = GetCalculatedTotals(cart);

            var deliveryMethodStr = HttpContext.Session.GetString("DeliveryMethod") ?? "ShipToHome";
            var storeId = HttpContext.Session.GetInt32("StoreId");
            var shipMethod = totals.shipMethod; // standard/express

            var order = new Order
            {
                UserId = user?.Id ?? "Guest",
                OrderDate = DateTime.UtcNow,

                Subtotal = totals.subtotal,
                DiscountAmount = cart.DiscountAmount < 0 ? 0 : cart.DiscountAmount,
                VatAmount = totals.vat,

                ShippingFee = totals.ship,
                ProvinceCode = totals.prov,
                DistrictCode = totals.dist,
                TotalPrice = totals.total,

                ShippingAddress = user?.Address ?? "N/A",
                Notes = $"Khách hàng: {user?.FullName ?? "Khách vãng lai"}. Thanh toán qua {paymentMethodName}. ShipMethod: {shipMethod}",
                DeliveryMethod = deliveryMethodStr == "PickupAtStore" ? DeliveryMethod.PickupAtStore : DeliveryMethod.ShipToHome,
                StoreId = storeId,
                CouponCode = cart.CouponCode
            };

            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync(); // có order.Id

            // 1) add all order details
            var details = new List<OrderDetail>();
            foreach (var item in cart.Items)
            {
                details.Add(new OrderDetail
                {
                    OrderId = order.Id,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price,
                    ProductName = item.Name,
                    VariantId = item.VariantId
                });
            }

            _dbContext.OrderDetails.AddRange(details);
            await _dbContext.SaveChangesAsync(); // có detail.Id

            // 2) add warranties (need detail.Id)
            var warranties = new List<OrderDetailWarranty>();
            for (int i = 0; i < cart.Items.Count; i++)
            {
                var item = cart.Items[i];
                var detail = details[i];

                if (item.Warranties == null) continue;

                foreach (var w in item.Warranties)
                {
                    warranties.Add(new OrderDetailWarranty
                    {
                        OrderDetailId = detail.Id,
                        WarrantyOptionId = w.WarrantyOptionId,
                        Name = w.Name,
                        Price = w.Price,
                        Months = w.Months
                    });
                }
            }

            if (warranties.Count > 0)
            {
                _dbContext.OrderDetailWarranties.AddRange(warranties);
                await _dbContext.SaveChangesAsync();
            }

            UpdateCouponUsageAfterOnlinePayment(cart, order.UserId);
            return order;
        }

        // ==================================================================================
        // 💳 VNPAY (KHÔNG tin amount từ client)
        // ==================================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult PaymentVNPay(
            decimal amount,
            string? deliveryMethod,
            int? storeId,
            string? provinceCode,
            string? districtCode,
            decimal? shippingFee,
            string? shipMethod) // ✅ added
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null) return RedirectToAction("Index", "ShoppingCart");

            SaveSessionInfo(deliveryMethod, storeId, provinceCode, districtCode, shippingFee, shipMethod);

            // ✅ Tính lại total từ server, bỏ amount từ client
            var totals = GetCalculatedTotals(cart);
            var safeAmount = totals.total;

            var vnpay = new VNPayLibrary();
            vnpay.AddRequestData("vnp_Version", _config["VNPay:Version"]);
            vnpay.AddRequestData("vnp_Command", _config["VNPay:Command"]);
            vnpay.AddRequestData("vnp_TmnCode", _config["VNPay:TmnCode"]);
            vnpay.AddRequestData("vnp_Amount", (Math.Round(safeAmount, 0) * 100).ToString("0", CultureInfo.InvariantCulture));
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", _config["VNPay:CurrCode"]);
            vnpay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString());
            vnpay.AddRequestData("vnp_Locale", _config["VNPay:Locale"]);
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang tai MobiStore");
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", _config["VNPay:ReturnUrl"]);
            vnpay.AddRequestData("vnp_TxnRef", DateTime.Now.Ticks.ToString());

            return Redirect(vnpay.CreateRequestUrl(_config["VNPay:PaymentUrl"], _config["VNPay:HashSecret"]));
        }

        public async Task<IActionResult> PaymentVNPayCallback()
        {
            if (Request.Query["vnp_ResponseCode"] != "00")
            {
                ViewBag.Message = "Thanh toán không thành công.";
                return View("PaymentCallBack");
            }

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null) return View("PaymentCallBack");

            try
            {
                var order = await CreateAndSaveOrder(cart, "VNPay");
                var user = User.Identity.IsAuthenticated ? await _userManager.GetUserAsync(User) : null;

                ViewBag.Message = "Success";
                ViewBag.OrderId = order.Id.ToString();
                ViewBag.OrderInfo = $"Khách hàng: {user?.FullName ?? "Khách hàng"}. Nội dung: Thanh toán VnPay thành công cho đơn hàng tại MobiStore";
                ViewBag.Amount = order.TotalPrice;

                HttpContext.Session.Remove("Cart");
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Lỗi khi lưu thông tin: " + ex.Message;
            }

            return View("PaymentCallBack");
        }

        // ==================================================================================
        // 💳 PAYPAL (KHÔNG tin amount từ client)
        // ==================================================================================

        public IActionResult Paypal(
            decimal? amount,
            string? deliveryMethod,
            int? storeId,
            string? provinceCode,
            string? districtCode,
            decimal? shippingFee,
            string? shipMethod) // ✅ added
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null) return RedirectToAction("Index", "ShoppingCart");

            SaveSessionInfo(deliveryMethod, storeId, provinceCode, districtCode, shippingFee, shipMethod);

            var totals = GetCalculatedTotals(cart);
            decimal totalVND = totals.total; // ✅ server-calculated
            decimal totalUSD = Math.Round(totalVND / 24000m, 2);

            ViewBag.TotalAmount = totalUSD.ToString("0.00", CultureInfo.InvariantCulture);
            ViewBag.TotalVND = totalVND;
            ViewBag.ClientId = _paypalSettings.Value.ClientId;
            ViewBag.Currency = _paypalSettings.Value.Currency;

            return View();
        }

        public async Task<IActionResult> PaypalSuccess()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null) return RedirectToAction("Index", "ShoppingCart");

            try
            {
                var order = await CreateAndSaveOrder(cart, "PayPal");
                var user = User.Identity.IsAuthenticated ? await _userManager.GetUserAsync(User) : null;

                ViewBag.Message = "PaypalSuccess";
                ViewBag.Amount = order.TotalPrice;
                ViewBag.OrderId = order.Id.ToString();
                ViewBag.OrderInfo = $"Khách hàng: {user?.FullName ?? "Khách hàng"}. Nội dung: Thanh toán PayPal thành công cho đơn hàng tại MobiStore";

                HttpContext.Session.Remove("Cart");
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Lỗi khi lưu thông tin: " + ex.Message;
            }

            return View("PaymentCallBack");
        }

        // ==================================================================================
        // 💳 MOMO (KHÔNG tin amount từ client)
        // ==================================================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePaymentMomo(
            OrderInfoModel model,
            string? deliveryMethod,
            int? storeId,
            string? provinceCode,
            string? districtCode,
            decimal? shippingFee,
            string? shipMethod) // ✅ added
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null) return RedirectToAction("Index", "ShoppingCart");

            SaveSessionInfo(deliveryMethod, storeId, provinceCode, districtCode, shippingFee, shipMethod);

            // ✅ set amount theo server
            var totals = GetCalculatedTotals(cart);
            model.Amount = (long)Math.Round(totals.total, 0);

            var response = await _momoService.CreatePaymentMomo(model, User.Identity.Name ?? "");
            if (response == null || response.ErrorCode != 0) return View("PaymentCallBack");

            return Redirect(response.PayUrl);
        }

        public async Task<IActionResult> PaymentCallBack()
        {
            var response = _momoService.PaymentExecuteAsync(Request.Query);
            if (response == null || string.IsNullOrEmpty(response.OrderId))
                return View("PaymentCallBack", response);

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null) return View("PaymentCallBack", response);

            try
            {
                var order = await CreateAndSaveOrder(cart, "MoMo");
                var user = User.Identity.IsAuthenticated ? await _userManager.GetUserAsync(User) : null;

                ViewBag.Message = "MomoSuccess";
                ViewBag.OrderId = order.Id.ToString();
                ViewBag.OrderInfo = $"Khách hàng: {user?.FullName ?? "Khách hàng"}. Nội dung: Thanh toán Momo thành công cho đơn hàng tại MobiStore";
                ViewBag.Amount = order.TotalPrice;

                HttpContext.Session.Remove("Cart");
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Lỗi khi lưu đơn hàng: " + ex.Message;
            }

            return View("PaymentCallBack", response);
        }

        // ==================================================================================
        // 🛠️ COUPON USAGE
        // ==================================================================================

        private void UpdateCouponUsageAfterOnlinePayment(ShoppingCart cart, string userId)
        {
            if (cart == null || string.IsNullOrWhiteSpace(cart.CouponCode) || userId == "Guest") return;

            var coupon = _dbContext.Coupons.SingleOrDefault(c => c.Code == cart.CouponCode);
            if (coupon == null) return;

            if (coupon.CurrentUsage < coupon.Quantity)
            {
                bool usedBefore = _dbContext.CouponUsages.Any(x => x.CouponId == coupon.Id && x.UserId == userId);
                if (!usedBefore)
                {
                    coupon.CurrentUsage++;
                    _dbContext.CouponUsages.Add(new CouponUsage
                    {
                        CouponId = coupon.Id,
                        UserId = userId,
                        UsedAt = DateTime.Now
                    });
                    _dbContext.SaveChanges();
                }
            }
        }
    }
}
