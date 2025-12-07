using Microsoft.AspNetCore.Mvc;
using WebBanDienThoai.Models;
using WebBanDienThoai.Services.Momo;
using WebBanDienThoai.Services.VNPay;
using WebBanDienThoai.Services;
using Microsoft.AspNetCore.Identity;
using WebBanDienThoai.Extensions; // Đảm bảo bạn có dịch vụ quản lý giỏ hàng
using Microsoft.Extensions.Options;
using System.Globalization;

namespace WebBanDienThoai.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IMomoService _momoService;
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOptions<PayPalSettings> _paypalSettings;
        private readonly ApplicationDbContext _dbContext;
        public PaymentController(IMomoService momoService, IConfiguration config, UserManager<ApplicationUser> userManager, IOptions<PayPalSettings> paypalSettings, ApplicationDbContext dbContext)
        {
            _momoService = momoService;
            _config = config;
            _userManager = userManager;
            _paypalSettings = paypalSettings; _dbContext = dbContext;
        }

        // Tạo thanh toán với MoMo
        [HttpPost]
        public async Task<IActionResult> CreatePaymentMomo(OrderInfoModel model)
        {
            var userName = "";

            if (HttpContext.User.Identity.IsAuthenticated)
            {
                userName = HttpContext.User.Identity.Name;
            }

            var response = await _momoService.CreatePaymentMomo(model, userName); // MomoCreatePaymentResponseModel

            // 1. Gọi API MoMo thất bại
            if (response == null)
            {
                ViewBag.Message = "Không gọi được API MoMo (response null).";
                // KHÔNG truyền response vì kiểu khác với View (Create vs Execute)
                return View("PaymentCallBack");
            }

            // 2. MoMo trả về mã lỗi
            if (response.ErrorCode != 0)
            {
                ViewBag.Message = $"Thanh toán MoMo thất bại: {response.Message} (ErrorCode = {response.ErrorCode})";
                return View("PaymentCallBack");
            }

            // 3. Không có PayUrl để redirect
            if (string.IsNullOrEmpty(response.PayUrl))
            {
                ViewBag.Message = "MoMo không trả về địa chỉ thanh toán (PayUrl).";
                return View("PaymentCallBack");
            }

            // 4. OK → chuyển sang trang thanh toán MoMo
            return Redirect(response.PayUrl);
        }

        // Callback từ MoMo khi thanh toán thành công hoặc thất bại
        [HttpGet]
        public IActionResult PaymentCallBack()
        {
            var query = HttpContext.Request.Query;

            // Ở đây response phải là MomoExecuteResponseModel
            var response = _momoService.PaymentExecuteAsync(query);

            // 1. Kiểm tra response từ MoMo
            if (response == null || string.IsNullOrEmpty(response.OrderId))
            {
                ViewBag.Message = "Thanh toán MoMo thất bại hoặc đã bị hủy.";
                return View("PaymentCallBack", response); // OK vì đúng kiểu model
            }

            // 2. Parse số tiền MoMo trả về
            if (!decimal.TryParse(response.Amount,
                                  NumberStyles.Any,
                                  CultureInfo.InvariantCulture,
                                  out var momoAmount))
            {
                ViewBag.Message = "Số tiền thanh toán MoMo không hợp lệ.";
                return View("PaymentCallBack", response);
            }

            // 3. Lấy giỏ hàng từ Session
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || cart.Items == null || !cart.Items.Any())
            {
                ViewBag.Message = "Không tìm thấy giỏ hàng trong phiên làm việc. Không thể tạo đơn từ thanh toán MoMo.";
                return View("PaymentCallBack", response);
            }

            // 4. Tính tiền giống PayPal: (giá + bảo hành) * số lượng
            decimal subtotal = cart.Items.Sum(i =>
            {
                decimal warrantyPerItem = i.Warranties?.Sum(w => w.Price) ?? 0m;
                return (i.Price + warrantyPerItem) * i.Quantity;
            });

            decimal discount = cart.DiscountAmount;
            if (discount < 0) discount = 0;

            decimal priceAfterDiscount = subtotal - discount;
            if (priceAfterDiscount < 0) priceAfterDiscount = 0;

            decimal vatAmount = Math.Round(priceAfterDiscount * 0.10m, 0);
            decimal total = priceAfterDiscount + vatAmount;   // Tổng cộng có VAT

            // (tuỳ chọn) có thể so sánh với momoAmount nếu muốn chắc chắn
            // if (total != momoAmount) { /* log cảnh báo hoặc xử lý thêm */ }

            // 5. Lấy thông tin user
            string fullName = "Khách chưa đăng nhập";
            string userId = "Guest";
            ApplicationUser? user = null;

            if (User.Identity.IsAuthenticated)
            {
                user = _userManager.GetUserAsync(User).Result;
                fullName = user?.FullName ?? User.Identity.Name;
                userId = user?.Id ?? "Guest";
            }

            // 6. Lấy DeliveryMethod + StoreId từ Session (giống PayPal)
            var deliveryMethodStr = HttpContext.Session.GetString("DeliveryMethod") ?? "ShipToHome";
            var deliveryMethod = deliveryMethodStr == "PickupAtStore"
                ? DeliveryMethod.PickupAtStore
                : DeliveryMethod.ShipToHome;

            int? storeIdFromSession = HttpContext.Session.GetInt32("StoreId");

            try
            {
                // 7. Lưu Order (đồng bộ fields với PayPal)
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalPrice = total,
                    ShippingAddress = user?.Address ?? "N/A",
                    Notes = "Thanh toán qua MoMo",
                    DeliveryMethod = deliveryMethod,
                    StoreId = storeIdFromSession,
                    Subtotal = subtotal,
                    DiscountAmount = discount,
                    VatAmount = vatAmount,
                    CouponCode = cart.CouponCode
                };
                _dbContext.Orders.Add(order);
                _dbContext.SaveChanges();

                // 8. Lưu giao dịch vào MomoInfoModel (dùng chung bảng transaction)
                var momoInfo = new MomoInfoModel
                {
                    OrderId = order.Id,
                    MomoOrderId = response.OrderId, // OrderId từ MoMo
                    OrderInfo = response.OrderInfo,
                    FullName = fullName,
                    Amount = total,            // hoặc momoAmount
                    DatePaid = DateTime.UtcNow
                };
                _dbContext.MomoInfos.Add(momoInfo);
                _dbContext.SaveChanges();

                // 9. Lưu chi tiết đơn hàng + bảo hành (như PayPalSuccess)
                foreach (var item in cart.Items)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price,
                        ProductName = item.Name,
                        VariantId = item.VariantId
                    };
                    _dbContext.OrderDetails.Add(orderDetail);
                    _dbContext.SaveChanges(); // để có Id cho OrderDetailWarranty

                    if (item.Warranties != null && item.Warranties.Any())
                    {
                        foreach (var w in item.Warranties)
                        {
                            var odw = new OrderDetailWarranty
                            {
                                OrderDetailId = orderDetail.Id,
                                WarrantyOptionId = w.WarrantyOptionId,
                                Name = w.Name,
                                Price = w.Price,
                                Months = w.Months
                            };
                            _dbContext.OrderDetailWarranties.Add(odw);
                        }
                        _dbContext.SaveChanges();
                    }
                }

                // 10. Xoá giỏ hàng
                cart.Items.Clear();
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                // 11. Thông tin trả về view
                ViewBag.Message = "MomoSuccess";
                ViewBag.Amount = total;
                ViewBag.OrderId = response.OrderId;
                ViewBag.OrderInfo = response.OrderInfo;
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Lỗi khi lưu thông tin thanh toán MoMo: " + ex.Message;
                return View("PaymentCallBack", response);
            }

            return View("PaymentCallBack", response);
        }



        // Thanh toán với VNPay
        [HttpPost]
        public IActionResult PaymentVNPay(decimal amount, string? deliveryMethod, int? storeId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || cart.Items == null || !cart.Items.Any())
            {
                return RedirectToAction("Index", "ShoppingCart");
            }

            // ⭐ LƯU phương thức nhận hàng + cửa hàng vào Session (y hệt PayPal)
            if (!string.IsNullOrEmpty(deliveryMethod))
            {
                HttpContext.Session.SetString("DeliveryMethod", deliveryMethod);
            }
            else
            {
                HttpContext.Session.SetString("DeliveryMethod", "ShipToHome");
            }

            if (storeId.HasValue && storeId.Value > 0)
            {
                HttpContext.Session.SetInt32("StoreId", storeId.Value);
            }
            else
            {
                HttpContext.Session.Remove("StoreId");
            }

            var vnpay = new VNPayLibrary();

            string vnp_Returnurl = _config["VNPay:ReturnUrl"];
            string vnp_Url = _config["VNPay:PaymentUrl"];
            string vnp_TmnCode = _config["VNPay:TmnCode"];
            string vnp_HashSecret = _config["VNPay:HashSecret"];

            // ⭐ amount đã là tham số decimal truyền vào (model binding từ field "Amount")
            // VNPay yêu cầu số nguyên * 100
            var roundedAmount = Math.Round(amount, 0);           // làm tròn nếu cần
            long amountVnp = (long)roundedAmount;             // VNPay dùng long

            string orderId = DateTime.Now.Ticks.ToString();
            string createDate = DateTime.Now.ToString("yyyyMMddHHmmss");

            vnpay.AddRequestData("vnp_Version", _config["VNPay:Version"]);
            vnpay.AddRequestData("vnp_Command", _config["VNPay:Command"]);
            vnpay.AddRequestData("vnp_TmnCode", vnp_TmnCode);
            vnpay.AddRequestData("vnp_Amount", (amountVnp * 100).ToString("0")); // ⭐ nhân 100
            vnpay.AddRequestData("vnp_CreateDate", createDate);
            vnpay.AddRequestData("vnp_CurrCode", _config["VNPay:CurrCode"]);
            vnpay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString());
            vnpay.AddRequestData("vnp_Locale", _config["VNPay:Locale"]);
            vnpay.AddRequestData("vnp_OrderInfo", "Thanh toán đơn hàng #" + orderId);
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", vnp_Returnurl);
            vnpay.AddRequestData("vnp_TxnRef", orderId);

            string paymentUrl = vnpay.CreateRequestUrl(vnp_Url, vnp_HashSecret);
            return Redirect(paymentUrl);
        }

        // Callback từ VNPay sau khi thanh toán thành công hoặc thất bại
        public IActionResult PaymentVNPayCallback()
        {
            // 1. Kiểm tra mã phản hồi từ VNPay
            string responseCode = Request.Query["vnp_ResponseCode"];
            if (responseCode != "00")
            {
                ViewBag.Message = $"Thanh toán thất bại hoặc bị huỷ (vnp_ResponseCode = {responseCode})";
                return View("PaymentCallBack");
            }

            // 2. Đọc các tham số VNPay trả về
            var vnpOrderId = Request.Query["vnp_TxnRef"].ToString();
            var orderInfo = Request.Query["vnp_OrderInfo"].ToString();
            var vnpAmountStr = Request.Query["vnp_Amount"].ToString(); // số tiền đã nhân 100

            if (!decimal.TryParse(vnpAmountStr, out var vnpAmountRaw))
            {
                ViewBag.Message = "Dữ liệu số tiền từ VNPay không hợp lệ.";
                return View("PaymentCallBack");
            }

            // VNPay trả về amount * 100 → chia lại cho 100
            var vnpAmount = vnpAmountRaw / 100m;

            // 3. Lấy giỏ hàng từ Session
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || cart.Items == null || !cart.Items.Any())
            {
                ViewBag.Message = "Không tìm thấy giỏ hàng trong phiên làm việc. Không thể tạo đơn VNPay.";
                return View("PaymentCallBack");
            }

            // 4. Tính tiền giống y PayPal
            decimal subtotal = cart.Items.Sum(i =>
            {
                decimal warrantyPerItem = i.Warranties?.Sum(w => w.Price) ?? 0m;
                return (i.Price + warrantyPerItem) * i.Quantity;
            });

            decimal discount = cart.DiscountAmount;
            if (discount < 0) discount = 0;

            decimal priceAfterDiscount = subtotal - discount;
            if (priceAfterDiscount < 0) priceAfterDiscount = 0;

            decimal vatAmount = Math.Round(priceAfterDiscount * 0.10m, 0);
            decimal total = priceAfterDiscount + vatAmount;   // Tổng cộng có VAT

            // (Tuỳ chọn) kiểm tra xem total có khớp với số tiền VNPay trả về không
            // Nếu bạn muốn strict hơn có thể so sánh và log:
            // if (total != vnpAmount) { /* log cảnh báo */ }

            // 5. Lấy thông tin user
            string fullName = "Khách chưa đăng nhập";
            string userId = "Guest";
            ApplicationUser? user = null;

            if (User.Identity.IsAuthenticated)
            {
                user = _userManager.GetUserAsync(User).Result;
                fullName = user?.FullName ?? User.Identity.Name;
                userId = user?.Id ?? "Guest";
            }

            // 6. Lấy phương thức nhận hàng + cửa hàng từ Session (giống PayPal)
            var deliveryMethodStr = HttpContext.Session.GetString("DeliveryMethod") ?? "ShipToHome";
            var deliveryMethod = deliveryMethodStr == "PickupAtStore"
                ? DeliveryMethod.PickupAtStore
                : DeliveryMethod.ShipToHome;

            int? storeIdFromSession = HttpContext.Session.GetInt32("StoreId");

            try
            {
                // 7. Lưu Order (giống PayPal)
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalPrice = total,
                    ShippingAddress = user?.Address ?? "N/A",
                    Notes = "Thanh toán qua VNPay",
                    DeliveryMethod = deliveryMethod,
                    StoreId = storeIdFromSession,
                    Subtotal = subtotal,
                    DiscountAmount = discount,
                    VatAmount = vatAmount,
                    CouponCode = cart.CouponCode
                };
                _dbContext.Orders.Add(order);
                _dbContext.SaveChanges();

                // 8. Lưu giao dịch (dùng chung bảng MomoInfoModel)
                var momoInfo = new MomoInfoModel
                {
                    OrderId = order.Id,
                    MomoOrderId = vnpOrderId, // vnp_TxnRef
                    OrderInfo = orderInfo,
                    FullName = fullName,
                    Amount = total,       // hoặc vnpAmount, tuỳ bạn muốn lấy số nào
                    DatePaid = DateTime.UtcNow
                };
                _dbContext.MomoInfos.Add(momoInfo);
                _dbContext.SaveChanges();

                // 9. Lưu chi tiết đơn hàng + bảo hành (giống PayPal)
                foreach (var item in cart.Items)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price,
                        ProductName = item.Name,
                        VariantId = item.VariantId
                    };
                    _dbContext.OrderDetails.Add(orderDetail);
                    _dbContext.SaveChanges(); // để có Id cho OrderDetailWarranty

                    if (item.Warranties != null && item.Warranties.Any())
                    {
                        foreach (var w in item.Warranties)
                        {
                            var odw = new OrderDetailWarranty
                            {
                                OrderDetailId = orderDetail.Id,
                                WarrantyOptionId = w.WarrantyOptionId,
                                Name = w.Name,
                                Price = w.Price,
                                Months = w.Months
                            };
                            _dbContext.OrderDetailWarranties.Add(odw);
                        }
                        _dbContext.SaveChanges();
                    }
                }

                // 10. Xoá giỏ hàng
                cart.Items.Clear();
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                // 11. Thông tin hiển thị ra view
                ViewBag.OrderId = vnpOrderId;
                ViewBag.OrderInfo = orderInfo;
                ViewBag.Amount = total;
                ViewBag.Message = "Success";
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Lỗi khi lưu thông tin thanh toán VNPay: " + ex.Message;
                return View("PaymentCallBack");
            }

            return View("PaymentCallBack");
        }

        public IActionResult Paypal(decimal? amount, string? deliveryMethod, int? storeId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || cart.Items == null || !cart.Items.Any())
            {
                return RedirectToAction("Index", "ShoppingCart");
            }

            // ⭐ LƯU phương thức nhận hàng + cửa hàng vào Session
            if (!string.IsNullOrEmpty(deliveryMethod))
            {
                HttpContext.Session.SetString("DeliveryMethod", deliveryMethod);
            }
            else
            {
                // mặc định nếu không có (phòng trường hợp URL không truyền)
                HttpContext.Session.SetString("DeliveryMethod", "ShipToHome");
            }

            if (storeId.HasValue && storeId.Value > 0)
            {
                HttpContext.Session.SetInt32("StoreId", storeId.Value);
            }
            else
            {
                HttpContext.Session.Remove("StoreId");
            }

            // ⭐ Tạm tính: (giá + bảo hành) * số lượng
            decimal subtotal = cart.Items.Sum(i =>
            {
                decimal warrantyPerItem = i.Warranties?.Sum(w => w.Price) ?? 0m;
                return (i.Price + warrantyPerItem) * i.Quantity;
            });

            decimal discount = cart.DiscountAmount;
            if (discount < 0) discount = 0;

            decimal priceAfterDiscount = subtotal - discount;
            if (priceAfterDiscount < 0) priceAfterDiscount = 0;

            decimal vatAmount = Math.Round(priceAfterDiscount * 0.10m, 0);
            decimal finalTotal = priceAfterDiscount + vatAmount;   // ⭐ Tổng cộng đúng như ở giỏ hàng

            // Nếu có amount truyền trên URL thì ưu tiên dùng (JS ở giỏ hàng đã set đúng finalTotal)
            decimal totalVND = amount.HasValue && amount.Value > 0
                ? amount.Value
                : finalTotal;

            // Quy đổi sang USD cho PayPal
            decimal totalUSD = Math.Round(totalVND / 24000m, 2);
            string totalAmountUSD = totalUSD.ToString("0.00", CultureInfo.InvariantCulture);

            ViewBag.TotalAmount = totalAmountUSD;
            ViewBag.TotalVND = totalVND;
            ViewBag.ClientId = _paypalSettings.Value.ClientId;
            ViewBag.Currency = _paypalSettings.Value.Currency;

            return View();
        }

        public async Task<IActionResult> PaypalSuccess()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || cart.Items == null || !cart.Items.Any())
            {
                return RedirectToAction("Index", "ShoppingCart");
            }

            // ⭐ Tạm tính: (giá + bảo hành) * số lượng
            decimal subtotal = cart.Items.Sum(i =>
            {
                decimal warrantyPerItem = i.Warranties?.Sum(w => w.Price) ?? 0m;
                return (i.Price + warrantyPerItem) * i.Quantity;
            });

            decimal discount = cart.DiscountAmount;
            if (discount < 0) discount = 0;

            decimal priceAfterDiscount = subtotal - discount;
            if (priceAfterDiscount < 0) priceAfterDiscount = 0;

            decimal vatAmount = Math.Round(priceAfterDiscount * 0.10m, 0);
            decimal total = priceAfterDiscount + vatAmount;   // ⭐ Tổng cộng có VAT

            var paypalOrderId = Guid.NewGuid().ToString("N").Substring(0, 12);

            string fullName = "Khách chưa đăng nhập";
            string userId = "Guest";
            ApplicationUser? user = null;

            if (User.Identity.IsAuthenticated)
            {
                user = await _userManager.GetUserAsync(User);
                fullName = user?.FullName ?? User.Identity.Name;
                userId = user?.Id ?? "Guest";
            }

            // ⭐ Lấy phương thức nhận hàng + cửa hàng từ Session (nếu đã lưu ở bước trước)
            var deliveryMethodStr = HttpContext.Session.GetString("DeliveryMethod") ?? "ShipToHome";
            var deliveryMethod = deliveryMethodStr == "PickupAtStore"
                ? DeliveryMethod.PickupAtStore
                : DeliveryMethod.ShipToHome;

            int? storeIdFromSession = HttpContext.Session.GetInt32("StoreId");

            try
            {
                // Lưu Order
                var order = new Order
                {
                    UserId = userId,
                    OrderDate = DateTime.UtcNow,
                    TotalPrice = total,                     // ✅ dùng tổng đã giảm + VAT
                    ShippingAddress = user?.Address ?? "N/A",
                    Notes = "Thanh toán qua PayPal",
                    DeliveryMethod = deliveryMethod,
                    StoreId = storeIdFromSession,           // nếu StoreId là int?
                    Subtotal = subtotal,
                    DiscountAmount = discount,
                    VatAmount = vatAmount,
                    CouponCode = cart.CouponCode
                };
                _dbContext.Orders.Add(order);
                _dbContext.SaveChanges();

                // Lưu vào bảng MomoInfoModel (dùng chung làm bảng giao dịch)
                var momoInfo = new MomoInfoModel
                {
                    OrderId = order.Id,
                    MomoOrderId = paypalOrderId,
                    OrderInfo = $"Khách hàng: {fullName}. Nội dung đơn hàng: Thanh toán PayPal tại WebBanDienThoai",
                    FullName = fullName,
                    Amount = total,
                    DatePaid = DateTime.UtcNow
                };
                _dbContext.MomoInfos.Add(momoInfo);
                _dbContext.SaveChanges();

                foreach (var item in cart.Items)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price,
                        ProductName = item.Name,
                        VariantId = item.VariantId
                    };
                    _dbContext.OrderDetails.Add(orderDetail);
                    _dbContext.SaveChanges(); // để lấy Id vừa tạo

                    if (item.Warranties != null && item.Warranties.Any())
                    {
                        foreach (var w in item.Warranties)
                        {
                            var odw = new OrderDetailWarranty
                            {
                                OrderDetailId = orderDetail.Id,
                                WarrantyOptionId = w.WarrantyOptionId,
                                Name = w.Name,
                                Price = w.Price,
                                Months = w.Months
                            };
                            _dbContext.OrderDetailWarranties.Add(odw);
                        }
                        _dbContext.SaveChanges();
                    }
                }

                // Xoá giỏ hàng
                cart.Items.Clear();
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                ViewBag.Message = "PaypalSuccess";
                ViewBag.Amount = total;
                ViewBag.OrderId = paypalOrderId;
                ViewBag.OrderInfo = $"Khách hàng: {fullName}. Nội dung đơn hàng: Thanh toán PayPal tại WebBanDienThoai";
            }
            catch (Exception ex)
            {
                ViewBag.Message = "Lỗi khi lưu thông tin thanh toán PayPal: " + ex.Message;
                return View("PaymentCallBack");
            }

            return View("PaymentCallBack");
        }



    }
}
