using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index(string searchTerm)
        {
            var users = _userManager.Users.ToList();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                users = users.Where(u =>
                    (u.FullName != null && u.FullName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)) ||
                    (u.Email != null && u.Email.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                ).ToList();

                ViewBag.CurrentFilter = searchTerm;
            }

            var userList = new List<(ApplicationUser User, string Role)>();
            foreach (var user in users)
            {
                var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Chưa phân quyền";
                userList.Add((user, role));
            }

            return View(userList);
        }


        public IActionResult Add()
        {
            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Add(ApplicationUser model, string password, string selectedRole)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    UserName = model.Email,
                    EmailConfirmed = true
                };

                // Tạo người dùng mới
                var result = await _userManager.CreateAsync(user, password);
                if (result.Succeeded)
                {
                    // Thêm người dùng vào vai trò
                    await _userManager.AddToRoleAsync(user, selectedRole);
                    return RedirectToAction("Index"); // Quay lại danh sách người dùng
                }

                // Thêm các lỗi vào ModelState nếu không thành công
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
            }

            // Trả lại danh sách vai trò trong ViewBag nếu có lỗi
            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Update(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault();

            ViewBag.Roles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.CurrentRole = currentRole;

            return View(user);
        }
        [HttpPost]
        public async Task<IActionResult> Update(ApplicationUser model, string selectedRole)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return Json(new { success = false, error = "Người dùng không tồn tại." });

            // Lấy người dùng hiện tại (Admin)
            var currentUser = await _userManager.GetUserAsync(User);

            // Nếu người dùng hiện tại là Admin và cố gắng thay đổi quyền của chính mình, không cho phép
            if (currentUser != null && currentUser.Id == user.Id)
            {
                return Json(new { success = false, error = "Bạn không thể thay đổi quyền của chính mình." });
            }

            // Cập nhật thông tin người dùng
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.UserName = model.Email;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError("", error.Description);

                return Json(new { success = false, error = "Có lỗi xảy ra khi cập nhật thông tin." });
            }

            // Cập nhật vai trò mới
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, selectedRole);

            // Trả về kết quả thành công
            return Json(new { success = true, message = "Cập nhật quyền thành công!" });
        }


        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            await _userManager.DeleteAsync(user);
            return RedirectToAction("Index");
        }


    }
}
