using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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

            ViewBag.Cart = cart;
            ViewBag.CurrentUser = user;

            ViewBag.Stores = await _context.Stores
                .Where(s => s.IsActive)
                .OrderBy(s => s.Province).ThenBy(s => s.District).ThenBy(s => s.Name)
                .ToListAsync();

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

            decimal subtotal = cart.Items.Sum(i =>
            {
                decimal warrantyPerItem = i.Warranties?.Sum(w => w.Price) ?? 0m;
                return (i.Price + warrantyPerItem) * i.Quantity;
            });

            var tradeInDiscountStr = HttpContext.Session.GetString("TradeInDiscount");
            decimal tradeInDiscount = 0m;
            decimal.TryParse(tradeInDiscountStr, out tradeInDiscount);

            decimal discount = cart.DiscountAmount < 0 ? 0 : cart.DiscountAmount;

            decimal afterDiscount = Math.Max(0, subtotal - discount - tradeInDiscount);
            decimal vatAmount = Math.Round(afterDiscount * 0.10m, 0);

            decimal shippingFee = 0m;
            if (order.DeliveryMethod == DeliveryMethod.ShipToHome)
            {
                shippingFee = await CalculateShippingFeeFromDbAsync(provinceCode, districtCode, shipMethod, afterDiscount);
            }

            decimal finalTotal = afterDiscount + vatAmount + shippingFee;

            order.UserId = user.Id;
            order.OrderDate = DateTime.UtcNow;
            order.Subtotal = subtotal;
            order.DiscountAmount = discount;
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

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

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

                if (!string.IsNullOrEmpty(cart.CouponCode))
                {
                    UpdateCouponUsageAfterOrder(cart.CouponCode, user.Id);
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["PaymentError"] = "Đã xảy ra sự cố trong quá trình ghi nhận đơn hàng: " + ex.Message;
                return RedirectToAction("Checkout");
            }

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

            return View(order);
        }

        [HttpGet]
        public IActionResult GetCartCount()
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            return Json(new { count = cart.Items.Count });
        }

        [Authorize]
        public async Task<IActionResult> AddToCart(int productId, int quantity, int? variantId)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null) return NotFound();

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            string name = product.Name;
            decimal price = product.DiscountedPrice;
            string imageUrl = product.ImageUrl;
            int stock;

            if (variantId.HasValue)
            {
                var variant = await _context.ProductVariants.FirstOrDefaultAsync(v => v.Id == variantId.Value);
                if (variant == null) return NotFound();

                name = $"{product.Name} ({variant.Capacity}, {variant.Color})";
                price = variant.DiscountedPrice;
                stock = variant.Stock;
            }
            else
            {
                stock = product.Quantity;
            }

            var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);

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
                    VariantId = variantId,
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

            var cart = new ShoppingCart();
            string name = product.Name;
            decimal price = product.DiscountedPrice;
            string imageUrl = product.ImageUrl;
            int stock;

            if (variantId.HasValue)
            {
                var variant = await _context.ProductVariants.AsNoTracking().FirstOrDefaultAsync(v => v.Id == variantId.Value);
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

            cart.CouponCode = null;
            cart.DiscountAmount = 0;

            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return RedirectToAction("Checkout");
        }

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

            var coupon = _context.Coupons.FirstOrDefault(c => c.Code.Trim().ToUpper() == couponCode.ToUpper());

            if (coupon == null || !coupon.IsActive || coupon.StartDate > now || coupon.EndDate < now)
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
                HttpContext.Session.SetObjectAsJson("Cart", cart);
                TempData["CouponError"] = "Mã giảm giá đã hết hạn hoặc không tồn tại.";
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

            bool hasUsed = _context.CouponUsages.Any(x => x.CouponId == coupon.Id && x.UserId == user.Id);
            if (hasUsed)
            {
                cart.CouponCode = null;
                cart.DiscountAmount = 0;
                HttpContext.Session.SetObjectAsJson("Cart", cart);
                TempData["CouponError"] = "Bạn đã sử dụng mã giảm giá này rồi.";
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
                TempData["CouponError"] = $"Đơn hàng phải tối thiểu {coupon.MinOrderValue:N0} VNĐ mới dùng được mã.";
                return RedirectToAction("Index");
            }

            decimal discount = 0;
            var percent = coupon.DiscountPercent ?? 0;
            var amount = coupon.DiscountAmount ?? 0m;

            if (percent > 0) discount = Math.Round(subtotal * percent / 100m, 0);
            else if (amount > 0) discount = amount;

            if (discount <= 0 || discount > subtotal) discount = subtotal;

            cart.CouponCode = coupon.Code;
            cart.DiscountAmount = discount;
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            TempData["CouponSuccess"] = $"Áp dụng mã {coupon.Code} thành công! Được giảm {discount:N0} VNĐ.";

            var referer = Request.Headers["Referer"].ToString();
            if (referer.Contains("Checkout", StringComparison.OrdinalIgnoreCase)) return RedirectToAction("Checkout");

            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("Admin") || User.IsInRole("Employer")) return RedirectToAction("Index", "Product");

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            foreach (var item in cart.Items)
            {
                int stock = 0;
                if (item.VariantId.HasValue)
                {
                    var variant = await _context.ProductVariants.AsNoTracking().FirstOrDefaultAsync(v => v.Id == item.VariantId.Value);
                    stock = variant?.Stock ?? 0;
                }
                else
                {
                    var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    stock = product?.Quantity ?? 0;
                }

                item.AvailableStock = stock;
                if (item.Quantity > stock) item.Quantity = stock;
            }
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            var suggestedProducts = _context.Products.OrderBy(p => Guid.NewGuid()).Take(6).ToList();
            ViewBag.SuggestedProducts = suggestedProducts;

            ApplicationUser? currentUser = null;
            if (User.Identity.IsAuthenticated) currentUser = await _userManager.GetUserAsync(User);
            ViewBag.CurrentUser = currentUser;

            var tradeInDiscountStr = HttpContext.Session.GetString("TradeInDiscount");
            var tradeInDeviceName = HttpContext.Session.GetString("TradeInDeviceName");
            decimal tradeInDiscount = 0m;
            decimal.TryParse(tradeInDiscountStr, out tradeInDiscount);

            ViewBag.TradeInDiscount = tradeInDiscount;
            ViewBag.TradeInDeviceName = tradeInDeviceName ?? "Thiết bị cũ";

            List<Coupon> availableCoupons = new List<Coupon>();
            if (currentUser != null)
            {
                var claimedCouponIds = await _context.CouponUsages.Where(u => u.UserId == currentUser.Id).Select(u => u.CouponId).Distinct().ToListAsync();
                availableCoupons = await _context.Coupons.Where(c => claimedCouponIds.Contains(c.Id) && c.IsActive && c.StartDate <= DateTime.Now && c.EndDate >= DateTime.Now && c.CurrentUsage < c.Quantity).OrderByDescending(c => c.DiscountPercent).ToListAsync();
            }
            ViewBag.AvailableCoupons = availableCoupons;

            ViewBag.WarrantyOptions = await _context.WarrantyOptions.Where(w => w.IsActive).OrderBy(w => w.Price).ToListAsync();
            ViewBag.Stores = await _context.Stores.Where(s => s.IsActive).OrderBy(s => s.Province).ThenBy(s => s.District).ThenBy(s => s.Name).ToListAsync();
            ViewBag.SaveLaterItems = (HttpContext.Session.GetObjectFromJson<ShoppingCart>("SaveLater") ?? new ShoppingCart()).Items;

            return View(cart);
        }

        [HttpPost]
        public IActionResult AddWarranty(int productId, int? variantId, int warrantyId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);
            if (item == null) return NotFound();

            var w = _context.WarrantyOptions.FirstOrDefault(x => x.Id == warrantyId);
            if (w == null) return NotFound();

            if (item.Warranties == null) item.Warranties = new List<CartItemWarranty>();
            if (!item.Warranties.Any(x => x.WarrantyOptionId == warrantyId))
            {
                item.Warranties.Add(new CartItemWarranty { WarrantyOptionId = w.Id, Name = w.Name, Price = w.Price });
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult UpdateWarranties(int productId, int? variantId, int[] warrantyOptionIds)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);
            if (item == null) return RedirectToAction("Index");

            item.Warranties.Clear();
            if (warrantyOptionIds != null && warrantyOptionIds.Length > 0)
            {
                var selected = _context.WarrantyOptions.Where(w => warrantyOptionIds.Contains(w.Id) && w.IsActive).ToList();
                foreach (var w in selected)
                {
                    item.Warranties.Add(new CartItemWarranty { WarrantyOptionId = w.Id, Name = w.Name, Price = w.Price });
                }
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult RemoveWarranty(int productId, int? variantId, int warrantyId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);
            if (item == null) return NotFound();

            if (item.Warranties != null) item.Warranties.RemoveAll(w => w.WarrantyOptionId == warrantyId);

            HttpContext.Session.SetObjectAsJson("Cart", cart);
            return Json(new { success = true });
        }

        private void UpdateCouponUsageAfterOrder(string? couponCode, string userId)
        {
            if (string.IsNullOrEmpty(couponCode) || string.IsNullOrEmpty(userId)) return;
            var coupon = _context.Coupons.SingleOrDefault(c => c.Code == couponCode);
            if (coupon == null || coupon.CurrentUsage >= coupon.Quantity) return;

            if (_context.CouponUsages.Any(x => x.CouponId == coupon.Id && x.UserId == userId)) return;

            coupon.CurrentUsage++;
            _context.CouponUsages.Add(new CouponUsage { CouponId = coupon.Id, UserId = userId, UsedAt = DateTime.Now });
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

            var rate = await _context.ShippingRates.AsNoTracking()
                .Where(x => x.ProvinceCode == prov && (x.DistrictCode == dist || x.DistrictCode == null))
                .OrderByDescending(x => x.DistrictCode != null).FirstOrDefaultAsync();

            if (rate == null) return 0m;
            decimal fee = (shipMethod == "express") ? rate.ExpressFee : rate.Fee;

            if (rate.FreeShipMinOrder != null && afterDiscount >= rate.FreeShipMinOrder.Value) fee = 0m;
            return fee;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCartQuantities([FromBody] List<CartQuantityUpdateDto> items)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            if (cart.Items == null || !cart.Items.Any() || items == null || items.Count == 0)
                return Json(new { success = false, message = "Dữ liệu trống." });

            bool hadCoupon = !string.IsNullOrEmpty(cart.CouponCode) && cart.DiscountAmount > 0;
            bool changedQty = false;

            foreach (var u in items)
            {
                var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == u.ProductId && i.VariantId == u.VariantId);
                if (cartItem == null) continue;

                int stock = 0;
                if (u.VariantId.HasValue)
                {
                    var variant = await _context.ProductVariants.AsNoTracking().FirstOrDefaultAsync(v => v.Id == u.VariantId.Value);
                    stock = variant?.Stock ?? 0;
                }
                else
                {
                    var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == u.ProductId);
                    stock = product?.Quantity ?? 0;
                }

                cartItem.AvailableStock = stock;
                if (stock <= 0) { cart.Items.Remove(cartItem); changedQty = true; continue; }

                int newQty = Math.Clamp(u.Quantity, 1, stock);
                if (cartItem.Quantity != newQty) { cartItem.Quantity = newQty; changedQty = true; }
            }

            if (hadCoupon && changedQty) { cart.CouponCode = null; cart.DiscountAmount = 0; }
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            return Json(new { success = true, count = cart.Items.Count });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearCart()
        {
            HttpContext.Session.Remove("Cart");
            TempData["SuccessMessage"] = "Đã xóa toàn bộ giỏ hàng.";
            return RedirectToAction("Index");
        }

        [Authorize]
        public IActionResult SaveForLater(int productId, int? variantId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var saveLater = HttpContext.Session.GetObjectFromJson<ShoppingCart>("SaveLater") ?? new ShoppingCart();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);
            if (item != null)
            {
                saveLater.Items.Add(item);
                cart.Items.Remove(item);
                if (!string.IsNullOrEmpty(cart.CouponCode)) { cart.CouponCode = null; cart.DiscountAmount = 0; }
                HttpContext.Session.SetObjectAsJson("Cart", cart);
                HttpContext.Session.SetObjectAsJson("SaveLater", saveLater);
            }
            return RedirectToAction("Index");
        }

        [Authorize]
        public IActionResult MoveBackToCart(int productId, int? variantId)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();
            var saveLater = HttpContext.Session.GetObjectFromJson<ShoppingCart>("SaveLater") ?? new ShoppingCart();
            var item = saveLater.Items.FirstOrDefault(i => i.ProductId == productId && i.VariantId == variantId);
            if (item != null)
            {
                cart.Items.Add(item);
                saveLater.Items.Remove(item);
                HttpContext.Session.SetObjectAsJson("Cart", cart);
                HttpContext.Session.SetObjectAsJson("SaveLater", saveLater);
            }
            return RedirectToAction("Index");
        }
    }
}