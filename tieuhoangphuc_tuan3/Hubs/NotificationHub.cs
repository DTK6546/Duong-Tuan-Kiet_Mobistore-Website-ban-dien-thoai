using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace WebBanDienThoai.Hubs
{
    public class NotificationHub : Hub
    {
        // Hàm này giúp Client (Browser) kết nối vào group riêng theo UserId của họ
        public async Task JoinUserGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }
    }
}