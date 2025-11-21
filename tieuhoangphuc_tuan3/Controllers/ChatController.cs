    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using WebBanDienThoai.Models;

    namespace WebBanDienThoai.Controllers
    {
        [Authorize]
        [ApiController]
        [Route("[controller]")]
        public class ChatController : ControllerBase
        {
            private readonly ApplicationDbContext _context;
            private readonly UserManager<ApplicationUser> _userManager;

            public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
            {
                _context = context;
                _userManager = userManager;
            }

            [HttpGet("LoadMessages")]
            public async Task<IActionResult> LoadMessages()
            {
                try
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    var userRole = (await _userManager.GetRolesAsync(currentUser)).FirstOrDefault();

                    // Lấy tất cả tin nhắn và sắp xếp
                    IQueryable<ChatMessage> query = _context.ChatMessages
                        .Include(m => m.User);

                    // Nếu là khách hàng, chỉ lấy tin nhắn của họ và admin
                    if (userRole == "Customer")
                    {
                        query = query.Where(m => m.UserId == currentUser.Id || m.Role == "Employer");
                    }

                    // Sắp xếp và chuyển đổi kết quả
                    var messages = await query
                        .OrderBy(m => m.Timestamp)
                        .Select(m => new
                        {
                            userName = m.UserName,
                            message = m.Message,
                            timestamp = m.Timestamp,
                            role = m.Role
                        })
                        .ToListAsync();

                    return Ok(messages);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = ex.Message });
                }
            }

            [HttpPost("SendMessage")]
            public async Task<IActionResult> SendMessage([FromBody] string message)
            {
                if (string.IsNullOrEmpty(message))
                {
                    return BadRequest("Tin nhắn không được để trống");
                }

                try
                {
                    var currentUser = await _userManager.GetUserAsync(User);
                    var userRole = (await _userManager.GetRolesAsync(currentUser)).FirstOrDefault();

                    var chatMessage = new ChatMessage
                    {
                        Message = message,
                        UserId = currentUser.Id,
                        UserName = currentUser.UserName,
                        Role = userRole ?? "Customer",
                        Timestamp = DateTime.Now
                    };

                    _context.ChatMessages.Add(chatMessage);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        userName = chatMessage.UserName,
                        message = chatMessage.Message,
                        timestamp = chatMessage.Timestamp,
                        role = chatMessage.Role
                    });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = ex.Message });
                }
            }
        }
    }