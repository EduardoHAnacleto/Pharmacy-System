using Microsoft.AspNetCore.SignalR;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Hubs
{
    public class PromotionsHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }

        public async Task NotifyPromotionsChanged()
        {
            await Clients.All.SendAsync("PromotionsChanged");
        }

        public async Task NotifyActivePromotionsChanged()
        {
            await Clients.All.SendAsync("ActivePromotionsChanged");
        }
    }
}
