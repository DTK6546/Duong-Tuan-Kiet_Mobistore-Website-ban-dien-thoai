// ChatHub.cs
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebBanDienThoai.Models;

namespace WebBanDienThoai.Services.SignalR
{
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatHub(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task SendMessage(string message)
        {
            var user = await _userManager.FindByIdAsync(Context.UserIdentifier);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var role = roles.Contains("Admin") ? "Admin" : "Customer";

                var chatMessage = new ChatMessage
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    Role = role
                };

                _context.ChatMessages.Add(chatMessage);
                await _context.SaveChangesAsync();

                await Clients.All.SendAsync("ReceiveMessage", user.UserName, message, chatMessage.Timestamp, role);
            }
        }

        public async Task LoadMessages()
        {
            var user = await _userManager.FindByIdAsync(Context.UserIdentifier);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var isEmployer = roles.Contains("Employer");
                var isAdmin = roles.Contains("Admin");

                var messages = await _context.ChatMessages
                    .Include(m => m.User)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                foreach (var message in messages)
                {
                    bool isOwn = message.UserId == user.Id;
                    bool isFromEmployer = message.Role == "Employer";
                    bool isToEmployer = isEmployer && message.Role == "Customer";

                    // Nếu là admin: xem hết
                    // Nếu là nhân viên: thấy khách + của mình
                    // Nếu là khách: thấy tin nhắn của mình + nhân viên
                    if (isAdmin || isOwn || isFromEmployer || isToEmployer)
                    {
                        await Clients.Caller.SendAsync("ReceiveMessage", message.UserName, message.Message, message.Timestamp, message.Role);
                    }
                }
            }
        }

    }
}
