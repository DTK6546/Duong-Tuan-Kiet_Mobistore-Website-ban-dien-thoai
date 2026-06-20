using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebBanDienThoai.Extensions;
using WebBanDienThoai.Models;
using WebBanDienThoai.Repositories;

namespace WebBanDienThoai.Controllers
{
    public class ShoppingCartController : Controller
    {
        private readonly IProductRepository _productRepository;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ShoppingCartController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IProductRepository productRepository)
        {
            _productRepository = productRepository;
            _context = context;
            _userManager = userManager;
        }

        // ================== CHECKOUT ==================

        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            if (cart.Items == null || !cart.Items.Any()) return RedirectToAction("Index");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // ĐỒNG BỘ: Đảm bảo số tiền giảm từ đổi điểm được nạp đầy đủ vào ViewBag
            ViewBag.Cart = cart;
            ViewBag.CurrentUser = user;

            ViewBag.Stores = await _context.Stores
                .Where(s => s.IsActive)
                .OrderBy(s => s.Province).ThenBy(s => s.District).ThenBy(s => s.Name)
                .ToListAsync();

            // =========================================================================
            // 💸 CẬP NHẬT CHỨC NĂNG 6: ĐỒNG BỘ ĐỌC KHẢO SÁT TRADE-IN SANG TRANG CHECKOUT
            // =========================================================================
            var tradeInDiscountStr = HttpContext.Session.GetString("TradeInDiscount");
            var tradeInDeviceName = HttpContext.Session.GetString("TradeInDeviceName");

            decimal tradeInDiscount = 0m;
            decimal.TryParse(tradeInDiscountStr, out tradeInDiscount);

            ViewBag.TradeInDiscount = tradeInDiscount;
            ViewBag.TradeInDeviceName = tradeInDeviceName ?? "Thiết bị cũ";

            return View(new Order { ShippingAddress = user.Address ?? "" });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(Order order)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart == null || !cart.Items.Any()) return RedirectToAction("Index");

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            // nhận từ form
            var deliveryMethod = Request.Form["DeliveryMethod"].ToString();
            var storeIdStr = Request.Form["StoreId"].ToString();
            var provinceCode = Request.Form["ProvinceCode"].ToString();
            var districtCode = Request.Form["DistrictCode"].ToString();
            var shipMethod = Request.Form["ShipMethod"].ToString();
            var paymentMethod = Request.Form["PaymentMethod"].ToString();

            order.DeliveryMethod = deliveryMethod == "PickupAtStore"
                ? DeliveryMethod.PickupAtStore
                : DeliveryMethod.ShipToHome;

            if (order.DeliveryMethod == DeliveryMethod.PickupAtStore)
            {
                if (!int.TryParse(storeIdStr, out var storeId))
                {
                    TempData["PaymentError"] = "Vui lòng chọn cửa hàng nhận.";
                    return RedirectToAction("Checkout");
                }
                order.StoreId = storeId;
                provinceCode = "";
                districtCode = "";
            }
            else
            {
                order.StoreId = null;
            }

            // subtotal (có bảo hành)
            decimal subtotal = cart.Items.Sum(i =>
            {
                decimal warrantyPerItem = i.Warranties?.Sum(w => w.Price) ?? 0m;
                return (i.Price + warrantyPerItem) * i.Quantity;
            });

            // =========================================================================
            // 💸 ĐỌC KHOẢN TIỀN TRỢ GIÁ THU CŨ ĐỔI MỚI TỪ SESSION ĐỂ KHẤU TRỪ
            // =========================================================================
            var tradeInDiscountStr = HttpContext.Session.GetString("TradeInDiscount");
            decimal tradeInDiscount = 0m;
            decimal.TryParse(tradeInDiscountStr, out tradeInDiscount);

            decimal discount = cart.DiscountAmount < 0 ? 0 : cart.DiscountAmount;

            // Giá trị chịu thuế = Tiền hàng - Mã giảm giá - Trợ giá máy cũ
            decimal afterDiscount = Math.Max(0, subtotal - discount - tradeInDiscount);
            decimal vatAmount = Math.Round(afterDiscount * 0.10m, 0);

            // shippingFee tính lại từ DB
            decimal shippingFee = 0m;
            if (order.DeliveryMethod == DeliveryMethod.ShipToHome)
            {
                shippingFee = await CalculateShippingFeeFromDbAsync(provinceCode, districtCode, shipMethod, afterDiscount);
            }

            // Tổng hóa đơn cuối cùng đã khấu trừ sạch sẽ các khoản
            decimal finalTotal = afterDiscount + vatAmount + shippingFee;

            order.UserId = user.Id;
            order.OrderDate = DateTime.UtcNow;

            order.Subtotal = subtotal;
            order.DiscountAmount = discount;

            // Gán khoản trợ giá máy cũ vào thực thể Order
            order.TradeInDiscount = tradeInDiscount;

            order.VatAmount = vatAmount;
            order.ShippingFee = shippingFee;
            order.ProvinceCode = provinceCode ?? "";
            order.DistrictCode = districtCode ?? "";
            order.CouponCode = cart.CouponCode;
            order.TotalPrice = finalTotal;

            if (paymentMethod == "Installment")
            {
                order.PaymentMethod = "Installment";
                order.PaymentStatus = PaymentStatus.ChuaThanhToan;
                order.InstallmentBank = Request.Form["InstallmentBank"].ToString();
                if (int.TryParse(Request.Form["InstallmentMonths"], out var months))
                {
                    order.InstallmentMonths = months;
                }
            }
            else
            {
                order.PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "COD" : paymentMethod;
                order.PaymentStatus = PaymentStatus.ChuaThanhToan;
            }

            order.Status = OrderStatus.ChoXacNhan;

            // =========================================================================
            // 🛡️ SỬ DỤNG TRANSACTION: Đảm bảo toàn vẹn dữ liệu hệ thống
            // =========================================================================
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync(); // Lưu trước để phát sinh và lấy order.Id

                foreach (var item in cart.Items)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        Price = item.Price,
                        VariantId = item.VariantId,
                        ProductName = item.Name
                    };

                    _context.OrderDetails.Add(orderDetail);
                    await _context.SaveChangesAsync();

                    // 🧭 TIẾN HÀNH TRỪ KHO THỰC TẾ
                    if (item.VariantId.HasValue)
                    {
                        var variant = await _context.ProductVariants.FindAsync(item.VariantId.Value);
                        if (variant != null)
                        {
                            variant.Stock = Math.Max(0, variant.Stock - item.Quantity);
                            _context.ProductVariants.Update(variant);
                        }
                    }
                    else
                    {
                        var dbProduct = await _context.Products.FindAsync(item.ProductId);
                        if (dbProduct != null)
                        {
                            dbProduct.Quantity = Math.Max(0, dbProduct.Quantity - item.Quantity);
                            _context.Products.Update(dbProduct);
                        }
                    }
                    await _context.SaveChangesAsync();

                    if (item.Warranties != null)
                    {
                        foreach (var w in item.Warranties)
                        {
                            _context.OrderDetailWarranties.Add(new OrderDetailWarranty
                            {
                                OrderDetailId = orderDetail.Id,
                                WarrantyOptionId = w.WarrantyOptionId,
                                Name = w.Name,
                                Price = w.Price,
                                Months = w.Months
                            });
                        }
                        await _context.SaveChangesAsync();
                    }
                }

                // Cập nhật trạng thái và lịch sử sử dụng Mã giảm giá công khai
                if (!string.IsNullOrEmpty(cart.CouponCode))
                {
                    UpdateCouponUsageAfterOrder(cart.CouponCode, user.Id);
                }

                // 🏁 HOÀN TẤT TRANSACTION: Đồng bộ ghi vĩnh viễn dữ liệu xuống DB
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                // 🔄 HOÀN TÁC (ROLLBACK): Nếu gãy ở bất cứ bước nào, xóa dữ liệu rác tạm thời ngay lập tức
                await transaction.RollbackAsync();
                TempData["PaymentError"] = "Đã xảy ra sự cố trong quá trình ghi nhận đơn hàng: " + ex.Message;
                return RedirectToAction("Checkout");
            }

            // 💾 DỌN SẠCH DỮ LIỆU TRONG SESSION
            HttpContext.Session.Remove("TradeInDiscount");
            HttpContext.Session.Remove("TradeInDeviceName");
            HttpContext.Session.Remove("Cart");

            return RedirectToAction("OrderCompleted", new { id = order.Id });
        }

        [Authorize]
        public async Task<IActionResult> OrderCompleted(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var order = await _context.Orders
                .AsNoTracking()
                .Where(o => o.Id == id && o.UserId == user.Id)
                .Include(o => o.Store)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Warranties)
                .FirstOrDefaultAsync();

            if (order == null) return NotFound();

            return View(order); // trả về Views/ShoppingCart/OrderCompleted.cshtml
        }

        // ================== GET CART COUNT (AJAX) ==================

        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            return Json(new { count = cart.Items.Count });
        }

        // ================== ADD TO CART ==================

        [Authorize]
        public async Task<IActionResult> AddToCart(int productId, int quantity, int? variantId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
                return NotFound();

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            string name = product.Name;
            decimal price = product.DiscountedPrice;
            string imageUrl = product.ImageUrl;
            int stock;

            // 🔹 Nếu có biến thể => dùng thông tin từ ProductVariant
            if (variantId.HasValue)
            {
                var variant = await _context.ProductVariants
                    .FirstOrDefaultAsync(v => v.Id == variantId.Value);

                if (variant == null)
                    return NotFound();

                name = $"{product.Name} ({variant.Capacity}, {variant.Color})";
                price = variant.DiscountedPrice;
                stock = variant.Stock;
            }
            else
            {
                stock = product.Quantity;  // ⭐ Tồn kho chung của sản phẩm
            }

            // Tìm item trong giỏ theo Product + Variant
            var cartItem = cart.Items.FirstOrDefault(i =>
                i.ProductId == productId && i.VariantId == variantId);

            if (cartItem != null)
            {
                int newQty = cartItem.Quantity + quantity;
                if (newQty > stock)
                {
                    newQty = stock;
                    TempData["PaymentError"] = $"Sản phẩm \"{name}\" chỉ còn {stock} sản phẩm.";
                }

                cartItem.Quantity = newQty;
                cartItem.Price = price;
                cartItem.AvailableStock = stock;
            }
            else
            {
                if (quantity > stock) quantity = stock;

                cart.Items.Add(new CartItem
                {
                    ProductId = product.Id,
                    VariantId = variantId,       // 🔹 Lưu lại variant
                    Name = name,
                    Price = price,
                    Quantity = quantity,
                    ImageUrl = imageUrl,
                    AvailableStock = stock
                });

                TempData["SuccessMessage"] = $"Đã thêm <strong>{name}</strong> vào giỏ hàng!";
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                Request.Headers["Accept"].ToString().Contains("application/json"))
                return Json(new { success = true });

            return RedirectToAction("Display", "Product", new { id = productId });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> BuyNow(int productId, int quantity, int? variantId)
        {
            if (quantity <= 0) quantity = 1;

            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null) return NotFound();

            // ✅ Tuỳ bạn: Mua ngay thường nên reset giỏ
            var cart = new ShoppingCart();
            // Nếu muốn GIỮ giỏ cũ thì dùng:
            // var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            string name = product.Name;
            decimal price = product.DiscountedPrice;
            string imageUrl = product.ImageUrl;
            int stock;

            if (variantId.HasValue)
            {
                var variant = await _context.ProductVariants
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.Id == variantId.Value);

                if (variant == null) return NotFound();

                name = $"{product.Name} ({variant.Capacity}, {variant.Color})";
                price = variant.DiscountedPrice;
                stock = variant.Stock;
            }
            else
            {
                stock = product.Quantity;
            }

            if (stock <= 0)
            {
                TempData["PaymentError"] = $"Sản phẩm \"{name}\" hiện đã hết hàng.";
                return RedirectToAction("Display", "Product", new { id = productId });
            }

            if (quantity > stock) quantity = stock;

            cart.Items.Add(new CartItem
            {
                ProductId = product.Id,
                VariantId = variantId,
                Name = name,
                Price = price,
                Quantity = quantity,
                ImageUrl = imageUrl,
                AvailableStock = stock
            });

            // Reset coupon nếu bạn muốn (mua ngay thường không nên giữ coupon cũ)
            cart.CouponCode = null;
            cart.DiscountAmount = 0;

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            // ✅ Chuyển thẳng qua giỏ hàng để thanh toán
            return RedirectToAction("Checkout");
        }

        // ================== APPLY COUPON ==================

        [HttpPost]
        public IActionResult ApplyCoupon(string couponCode)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            if (cart.Items == null || !cart.Items.Any())
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                TempData["CouponError"] = "Giỏ hàng đang trống, không thể áp dụng mã giảm giá.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(couponCode))
            {
                TempData["CouponError"] = "Vui lòng nhập mã giảm giá.";
                return RedirectToAction("Index");
            }

            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
            {
                TempData["CouponError"] = "Bạn cần đăng nhập để sử dụng mã giảm giá.";
                return RedirectToAction("Login", "Account");
            }

            couponCode = couponCode.Trim();
            var now = DateTime.Now;

            // Tìm chính xác mã Voucher sinh ra từ hệ thống đổi điểm
            var coupon = _context.Coupons.FirstOrDefault(c => c.Code.Trim().ToUpper() == couponCode.ToUpper());

            if (!coupon.IsActive || coupon.StartDate > DateTime.Now.AddDays(1) || coupon.EndDate < DateTime.Now.AddDays(-1))
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                TempData["CouponError"] = "Mã giảm giá đã hết hạn hoặc chưa đến thời gian áp dụng.";
                return RedirectToAction("Index");
            }

            if (!coupon.IsActive || coupon.StartDate > now || coupon.EndDate < now)
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                TempData["CouponError"] = "Mã giảm giá đã hết hạn hoặc chưa đến thời gian áp dụng.";
                return RedirectToAction("Index");
            }

            if (coupon.CurrentUsage >= coupon.Quantity)
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                TempData["CouponError"] = "Mã giảm giá này đã được sử dụng hết lượt.";
                return RedirectToAction("Index");
            }

            bool hasUsed = _context.CouponUsages
                .Any(x => x.CouponId == coupon.Id && x.UserId == user.Id);

            if (hasUsed)
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                TempData["CouponError"] = "Bạn đã sử dụng mã giảm giá này cho một đơn hàng trước đó rồi.";
                return RedirectToAction("Index");
            }

            decimal subtotal = cart.Items.Sum(i =>
            {
                decimal warrantyPerItem = i.Warranties?.Sum(w => w.Price) ?? 0m;
                return (i.Price + warrantyPerItem) * i.Quantity;
            });

            if (subtotal < coupon.MinOrderValue)
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                TempData["CouponError"] = $"Đơn hàng phải tối thiểu {coupon.MinOrderValue:N0} VNĐ mới dùng được mã này.";
                return RedirectToAction("Index");
            }

            decimal discount = 0;
            var percent = coupon.DiscountPercent ?? 0;
            var amount = coupon.DiscountAmount ?? 0m;

            if (percent > 0)
            {
                discount = Math.Round(subtotal * percent / 100m, 0);
            }
            else if (amount > 0)
            {
                discount = amount;
            }

            if (discount <= 0)
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
                HttpContext.Session.SetObjectAsJson("Cart", cart);

                TempData["CouponError"] = "Mã giảm giá không hợp lệ cho các sản phẩm hiện tại.";
                return RedirectToAction("Index");
            }

            if (discount > subtotal)
            {
                discount = subtotal;
            }

            // 💾 LƯU CHẶT VÀO CART SESSION
            cart.CouponCode = coupon.Code;
            cart.DiscountAmount = discount;
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            TempData["CouponSuccess"] = $"Áp dụng mã {coupon.Code} thành công! Bạn được giảm {discount:N0} VNĐ.";

            // ✨ THAY ĐỔI CHIẾN LƯỢC: Kiểm tra nếu khách đang thao tác ở trang checkout thì quay lại checkout
            var referer = Request.Headers["Referer"].ToString();
            if (referer.Contains("Checkout", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Checkout");
            }

            return RedirectToAction("Index");
        }


        // ================== INDEX (TRANG GIỎ HÀNG) ==================

        public async Task<IActionResult> Index()
        {
            // Nếu là Admin hoặc Employer thì chuyển sang trang quản trị
            if (User.IsInRole("Admin") || User.IsInRole("Employer"))
            {
                return RedirectToAction("Index", "Product");
            }

            // 🔹 Lấy giỏ hàng từ Session (nếu trống thì tạo mới)
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            // 🔹 Cập nhật tồn kho mới nhất cho từng sản phẩm (đúng cả biến thể)
            foreach (var item in cart.Items)
            {
                int stock = 0;

                if (item.VariantId.HasValue)
                {
                    var variant = await _context.ProductVariants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.Id == item.VariantId.Value);

                    stock = variant?.Stock ?? 0;
                }
                else
                {
                    var product = await _context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                    stock = product?.Quantity ?? 0;
                }

                item.AvailableStock = stock;

                if (item.Quantity > stock)
                {
                    item.Quantity = stock;
                    TempData["PaymentError"] =
                        $"Số lượng sản phẩm \"{item.Name}\" trong giỏ đã được giảm theo tồn kho hiện tại ({stock}).";
                }
            }
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            // 🔹 Gợi ý sản phẩm ngẫu nhiên
            var suggestedProducts = _context.Products
                .OrderBy(p => Guid.NewGuid())
                .Take(6)
                .ToList();
            ViewBag.SuggestedProducts = suggestedProducts;

            // 🔹 Lấy thông tin người dùng hiện tại (nếu đã đăng nhập)
            ApplicationUser? currentUser = null;
            if (User.Identity.IsAuthenticated)
            {
                currentUser = await _userManager.GetUserAsync(User);
            }
            ViewBag.CurrentUser = currentUser;

            // =========================================================================
            // 💸 CẬP NHẬT CHỨC NĂNG 6: BỐC DỮ LIỆU TRADE-IN TỪ SESSION RA VIEW
            // =========================================================================
            var tradeInDiscountStr = HttpContext.Session.GetString("TradeInDiscount");
            var tradeInDeviceName = HttpContext.Session.GetString("TradeInDeviceName");

            decimal tradeInDiscount = 0m;
            decimal.TryParse(tradeInDiscountStr, out tradeInDiscount);

            ViewBag.TradeInDiscount = tradeInDiscount;
            ViewBag.TradeInDeviceName = tradeInDeviceName ?? "Thiết bị cũ";

            // ==============================
            // ⭐ DANH SÁCH MÃ GIẢM GIÁ ĐÃ NHẬN (Level 2)
            // ==============================
            List<Coupon> availableCoupons = new List<Coupon>();

            if (currentUser != null)
            {
                var userId = currentUser.Id;

                // Lấy các CouponId mà user này đã nhận (ClaimCoupon)
                var claimedCouponIds = await _context.CouponUsages
                    .Where(u => u.UserId == userId)
                    .Select(u => u.CouponId)
                    .Distinct()
                    .ToListAsync();

                // Chỉ lấy những coupon user đã nhận + còn hiệu lực
                availableCoupons = await _context.Coupons
                    .Where(c =>
                        claimedCouponIds.Contains(c.Id) &&      // chỉ các mã user đã nhận
                        c.IsActive &&
                        c.StartDate <= DateTime.Now &&
                        c.EndDate >= DateTime.Now &&
                        c.CurrentUsage < c.Quantity)            // còn lượt
                    .OrderByDescending(c => c.DiscountPercent)
                    .ToListAsync();
            }
            else
            {
                // Chưa đăng nhập => không hiện mã nào
                availableCoupons = new List<Coupon>();
            }

            ViewBag.AvailableCoupons = availableCoupons;

            // 🔹 Danh sách gói bảo hành đang hoạt động
            var warrantyOptions = await _context.WarrantyOptions
                .Where(w => w.IsActive)
                .OrderBy(w => w.Price)
                .ToListAsync();
            ViewBag.WarrantyOptions = warrantyOptions;

            // 🔹 Danh sách cửa hàng đang hoạt động
            ViewBag.Stores = await _context.Stores
                .Where(s => s.IsActive)
                .OrderBy(s => s.Province)
                .ThenBy(s => s.District)
                .ThenBy(s => s.Name)
                .ToListAsync();

            var saveLater = HttpContext.Session.GetObjectFromJson<ShoppingCart>("SaveLater") ?? new ShoppingCart();
            ViewBag.SaveLaterItems = saveLater.Items;

            // 🔹 Trả view với giỏ hàng hiện tại
            return View(cart);
        }

        [HttpPost]
        public IActionResult AddWarranty(int productId, int? variantId, int warrantyId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var item = cart.Items.FirstOrDefault(i =>
        i.ProductId == productId &&
        i.VariantId == variantId);

            if (item == null)
                return NotFound();

            var w = _context.WarrantyOptions.FirstOrDefault(x => x.Id == warrantyId);
            if (w == null)
                return NotFound();

            // Nếu chưa có danh sách bảo hành → tạo mới
            if (item.Warranties == null)
                item.Warranties = new List<CartItemWarranty>();

            // Nếu bảo hành này đã có thì không thêm trùng
            if (!item.Warranties.Any(x => x.WarrantyOptionId == warrantyId))
            {
                item.Warranties.Add(new CartItemWarranty
                {
                    WarrantyOptionId = w.Id,
                    Name = w.Name,
                    Price = w.Price
                });
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult UpdateWarranties(int productId, int? variantId, int[] warrantyOptionIds)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var item = cart.Items.FirstOrDefault(i =>
        i.ProductId == productId &&
        i.VariantId == variantId);
            if (item == null)
                return RedirectToAction("Index");

            item.Warranties.Clear();

            if (warrantyOptionIds != null && warrantyOptionIds.Length > 0)
            {
                var selected = _context.WarrantyOptions
                    .Where(w => warrantyOptionIds.Contains(w.Id) && w.IsActive)
                    .ToList();

                foreach (var w in selected)
                {
                    item.Warranties.Add(new CartItemWarranty
                    {
                        WarrantyOptionId = w.Id,
                        Name = w.Name,
                        Price = w.Price
                    });
                }
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RemoveWarranty(int productId, int? variantId, int warrantyId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var item = cart.Items.FirstOrDefault(i =>
    i.ProductId == productId && i.VariantId == variantId);

            if (item == null)
                return NotFound();

            if (item.Warranties != null)
            {
                item.Warranties.RemoveAll(w => w.WarrantyOptionId == warrantyId);
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return Json(new { success = true });
        }


        // ================== HÀM PHỤ ==================

        private async Task<Product> GetProductFromDatabase(int productId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            return product;
        }

        private void UpdateCouponUsageAfterOrder(string? couponCode, string userId)
        {
            if (string.IsNullOrEmpty(couponCode)) return;
            if (string.IsNullOrEmpty(userId)) return;

            var coupon = _context.Coupons.SingleOrDefault(c => c.Code == couponCode);
            if (coupon == null) return;

            // Hết lượt thì thôi, không cập nhật nữa
            if (coupon.CurrentUsage >= coupon.Quantity) return;

            // User đã từng dùng mã này rồi -> không ghi thêm (phòng trùng)
            bool hasUsed = _context.CouponUsages
                .Any(x => x.CouponId == coupon.Id && x.UserId == userId);
            if (hasUsed) return;

            // Tăng lượt đã dùng
            coupon.CurrentUsage++;

            // Lưu lịch sử sử dụng
            _context.CouponUsages.Add(new CouponUsage
            {
                CouponId = coupon.Id,
                UserId = userId,
                UsedAt = DateTime.Now
            });

            _context.SaveChanges();
        }

        public IActionResult RemoveFromCart(int productId, int? variantId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");
            if (cart is not null)
            {
                cart.RemoveItem(productId, variantId);
                HttpContext.Session.SetObjectAsJson("Cart", cart);
            }
            return RedirectToAction("Index");
        }

        private async Task<decimal> CalculateShippingFeeFromDbAsync(string prov, string? dist, string shipMethod, decimal afterDiscount)
        {
            if (string.IsNullOrWhiteSpace(prov)) return 0m;

            prov = prov.Trim().ToUpper();
            dist = string.IsNullOrWhiteSpace(dist) ? null : dist.Trim().ToUpper();
            shipMethod = (shipMethod ?? "standard").Trim().ToLower();

            var rate = await _context.ShippingRates
                .AsNoTracking()
                .Where(x => x.ProvinceCode == prov && (x.DistrictCode == dist || x.DistrictCode == null))
                .OrderByDescending(x => x.DistrictCode != null)
                .FirstOrDefaultAsync();

            if (rate == null) return 0m;

            bool isExpress = shipMethod == "express" || shipMethod == "nhanh";
            decimal fee = isExpress ? rate.ExpressFee : rate.Fee;

            if (rate.FreeShipMinOrder != null && afterDiscount >= rate.FreeShipMinOrder.Value)
                fee = 0m;

            return fee;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCartQuantities([FromBody] List<CartQuantityUpdateDto> items)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            if (cart.Items == null || !cart.Items.Any())
                return Json(new { success = false, message = "Giỏ hàng đang trống." });

            if (items == null || items.Count == 0)
                return Json(new { success = false, message = "Không có dữ liệu cập nhật." });

            bool hadCoupon = !string.IsNullOrEmpty(cart.CouponCode) && cart.DiscountAmount > 0;
            bool changedQty = false;
            string? warning = null;

            foreach (var u in items)
            {
                var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == u.ProductId && i.VariantId == u.VariantId);
                if (cartItem == null) continue;

                // lấy tồn kho mới nhất
                int stock = 0;
                if (u.VariantId.HasValue)
                {
                    var variant = await _context.ProductVariants
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.Id == u.VariantId.Value);
                    stock = variant?.Stock ?? 0;
                }
                else
                {
                    var product = await _context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == u.ProductId);
                    stock = product?.Quantity ?? 0;
                }

                cartItem.AvailableStock = stock;

                // hết hàng => remove khỏi giỏ
                if (stock <= 0)
                {
                    cart.Items.Remove(cartItem);
                    changedQty = true;
                    continue;
                }

                int newQty = u.Quantity;
                if (newQty < 1) newQty = 1;
                if (newQty > stock)
                {
                    newQty = stock;
                    warning = $"Một số sản phẩm đã được giảm số lượng theo tồn kho hiện tại ({stock}).";
                }

                if (cartItem.Quantity != newQty)
                {
                    cartItem.Quantity = newQty;
                    changedQty = true;
                }
            }

            // Nếu có mã giảm giá và người dùng thay đổi số lượng => xoá mã để tránh lệch tiền
            if (hadCoupon && changedQty)
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            var msg = changedQty ? "Đã cập nhật giỏ hàng." : "Không có thay đổi.";
            if (hadCoupon && changedQty)
                msg += " (Mã giảm giá đã được gỡ, vui lòng áp lại.)";

            if (!string.IsNullOrEmpty(warning))
                msg += " " + warning;

            return Json(new
            {
                success = true,
                message = msg,
                count = cart.Items.Count
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            TempData["SuccessMessage"] = "Đã xóa toàn bộ giỏ hàng.";
            return RedirectToAction("Index");
        }

        // =========================================================================
        // ✨ CHỨC NĂNG: LƯU SẢN PHẨM LẠI ĐỂ MUA SAU (SAVE FOR LATER)
        // =========================================================================
        [Authorize]
        public IActionResult SaveForLater(int productId, int? variantId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var saveLater = HttpContext.Session.GetObjectFromJson<ShoppingCart>("SaveLater") ?? new ShoppingCart();

            // Tìm item đang có trong giỏ hàng chính
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);
            if (item != null)
            {
                // 1. Thêm vào danh sách lưu sau
                saveLater.Items.Add(item);
                // 2. Xóa khỏi giỏ hàng hiện tại
                cart.Items.Remove(item);

                // Reset lại mã giảm giá nếu giỏ hàng thay đổi để tránh lệch tiền
                if (!string.IsNullOrEmpty(cart.CouponCode))
                {
                    cart.CouponCode = null;
                    cart.DiscountAmount = 0;
                }

                // Cập nhật lại Session của cả 2 giỏ
                HttpContext.Session.SetObjectAsJson("Cart", cart);
                HttpContext.Session.SetObjectAsJson("SaveLater", saveLater);

                TempData["SuccessMessage"] = $"Đã lưu sản phẩm '{item.Name}' để mua sau.";
            }

            return RedirectToAction("Index");
        }

        // =========================================================================
        // ✨ CHỨC NĂNG: CHUYỂN SẢN PHẨM ĐÃ LƯU TRỞ LẠI GIỎ HÀNG CHÍNH (MOVE BACK TO CART)
        // =========================================================================
        [Authorize]
        public IActionResult MoveBackToCart(int productId, int? variantId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var saveLater = HttpContext.Session.GetObjectFromJson<ShoppingCart>("SaveLater") ?? new ShoppingCart();

            // Tìm sản phẩm trong danh sách lưu sau
            var item = saveLater.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);
            if (item != null)
            {
                // 1. Đẩy ngược lại vào giỏ hàng chính
                cart.Items.Add(item);
                // 2. Xóa khỏi danh sách lưu sau
                saveLater.Items.Remove(item);

                HttpContext.Session.SetObjectAsJson("Cart", cart);
                HttpContext.Session.SetObjectAsJson("SaveLater", saveLater);

                TempData["SuccessMessage"] = $"Đã chuyển '{item.Name}' trở lại giỏ hàng.";
            }

            return RedirectToAction("Index");
        }
    }
}
