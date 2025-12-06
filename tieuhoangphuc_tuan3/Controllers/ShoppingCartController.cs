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

        public IActionResult Checkout()
        {
            // View Checkout hiện đang dùng Order, phần hiển thị tạm thời bạn giữ nguyên
            return View(new Order());
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(Order order)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart");

            if (cart == null || !cart.Items.Any())
                return RedirectToAction("Index");

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            // Lấy phương thức nhận hàng
            var deliveryMethod = Request.Form["DeliveryMethod"].ToString();
            var storeIdStr = Request.Form["StoreId"].ToString();

            order.DeliveryMethod = deliveryMethod == "PickupAtStore"
                                   ? DeliveryMethod.PickupAtStore
                                   : DeliveryMethod.ShipToHome;

            if (int.TryParse(storeIdStr, out int storeId))
                order.StoreId = storeId;

            // Tính tổng
            decimal subtotal = cart.Items.Sum(i =>
            {
                decimal warrantyPerItem = i.Warranties?.Sum(w => w.Price) ?? 0m;
                return (i.Price + warrantyPerItem) * i.Quantity;
            });

            decimal discount = cart.DiscountAmount < 0 ? 0 : cart.DiscountAmount;
            decimal priceAfterDiscount = Math.Max(0, subtotal - discount);

            decimal vatAmount = Math.Round(priceAfterDiscount * 0.10m, 0);
            decimal finalTotal = priceAfterDiscount + vatAmount;

            order.UserId = user.Id;
            order.OrderDate = DateTime.UtcNow;
            order.TotalPrice = finalTotal;
            order.Subtotal = subtotal;
            order.DiscountAmount = discount;
            order.VatAmount = vatAmount;
            order.CouponCode = cart.CouponCode;
            order.TotalPrice = finalTotal;

            // ⭐ LƯU ORDER TRƯỚC
            _context.Orders.Add(order);
            await _context.SaveChangesAsync(); // Order.Id được tạo TẠI ĐÂY

            // ⭐ GIỜ mới lưu chi tiết đơn hàng
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

                // Lưu bảo hành
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

            HttpContext.Session.Remove("Cart");

            return View("OrderCompleted", order.Id);
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


        // ================== APPLY COUPON ==================

        [HttpPost]
        public IActionResult ApplyCoupon(string couponCode)
        {
            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            if (cart.Items == null || !cart.Items.Any())
            {
                TempData["CouponError"] = "Giỏ hàng đang trống, không thể áp dụng mã giảm giá.";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(couponCode))
            {
                TempData["CouponError"] = "Vui lòng nhập mã giảm giá.";
                return RedirectToAction("Index");
            }

            couponCode = couponCode.Trim();
            var now = DateTime.Now;

            var coupon = _context.Coupons
                .FirstOrDefault(c =>
                    c.Code == couponCode &&
                    c.IsActive &&
                    c.StartDate <= now &&
                    c.EndDate >= now);

            if (coupon == null)
            {
                TempData["CouponError"] = "Mã giảm giá không tồn tại hoặc đã hết hạn.";
                return RedirectToAction("Index");
            }

            var subtotal = cart.Items.Sum(i => i.Price * i.Quantity);

            if (subtotal < coupon.MinOrderValue)
            {
                TempData["CouponError"] =
                    $"Đơn hàng phải tối thiểu {coupon.MinOrderValue:N0} VNĐ mới dùng được mã này.";
                return RedirectToAction("Index");
            }

            if (coupon.Quantity <= 0)
            {
                TempData["CouponError"] = "Mã giảm giá này đã được sử dụng hết.";
                return RedirectToAction("Index");
            }

            // ✅ Tính số tiền giảm (xử lý nullable)
            decimal discount = 0;

            var percent = coupon.DiscountPercent ?? 0;   // int? -> int
            var amount = coupon.DiscountAmount ?? 0m;   // decimal? -> decimal

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
                TempData["CouponError"] = "Mã giảm giá hiện không áp dụng được cho đơn hàng này.";
                return RedirectToAction("Index");
            }

            // Không cho giảm quá tổng
            if (discount > subtotal)
            {
                discount = subtotal;
            }

            // ✅ Lưu vào cart (session)
            cart.CouponCode = coupon.Code;
            cart.DiscountAmount = discount;
            HttpContext.Session.SetObjectAsJson("Cart", cart);

            // Trừ bớt số lượng mã
            coupon.Quantity -= 1;
            _context.SaveChanges();

            TempData["CouponSuccess"] =
                $"Áp dụng mã {coupon.Code} thành công! Bạn được giảm {discount:N0} VNĐ.";

            return RedirectToAction("Index");
        }


        // ================== INDEX (TRANG GIỎ HÀNG) ==================

        public IActionResult Index()
        {
            if (User.IsInRole("Admin") || User.IsInRole("Employer"))
            {
                return RedirectToAction("Index", "Product");
            }

            var cart = HttpContext.Session.GetObjectFromJson<ShoppingCart>("Cart") ?? new ShoppingCart();

            // ⭐ Cập nhật tồn kho mới nhất
            foreach (var item in cart.Items)
            {
                var product = _context.Products.FirstOrDefault(p => p.Id == item.ProductId);
                if (product != null)
                {
                    int stock = product.Quantity;
                    item.AvailableStock = stock;

                    if (item.Quantity > stock)
                    {
                        item.Quantity = stock;
                        TempData["PaymentError"] = $"Số lượng sản phẩm \"{product.Name}\" trong giỏ đã được giảm theo tồn kho hiện tại ({stock}).";
                    }
                }
            }

            HttpContext.Session.SetObjectAsJson("Cart", cart);

            var suggestedProducts = _context.Products
                .OrderBy(p => Guid.NewGuid())
                .Take(6)
                .ToList();
            ViewBag.SuggestedProducts = suggestedProducts;

            var availableCoupons = _context.Coupons
                .Where(c => c.IsActive &&
                            c.StartDate <= DateTime.Now &&
                            c.EndDate >= DateTime.Now &&
                            c.Quantity > 0)
                .OrderByDescending(c => c.DiscountPercent)
                .ToList();
            ViewBag.AvailableCoupons = availableCoupons;

            var warrantyOptions = _context.WarrantyOptions
                .Where(w => w.IsActive)
                .OrderBy(w => w.Price)
                .ToList();
            ViewBag.WarrantyOptions = warrantyOptions;

            // ⭐ LẤY THÔNG TIN USER HIỆN TẠI (NẾU ĐÃ ĐĂNG NHẬP)
            var currentUser = _userManager.GetUserAsync(User).Result;
            ViewBag.CurrentUser = currentUser;

            ViewBag.Stores = _context.Stores
    .Where(s => s.IsActive)
    .OrderBy(s => s.Province)
    .ThenBy(s => s.District)
    .ThenBy(s => s.Name)
    .ToList();

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
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

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

    }
}
