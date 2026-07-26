using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Storefront.Api.Models;

namespace Storefront.Api.Hubs
{
    /// <summary>
    /// Pushes promotion changes to connected storefronts.
    /// </summary>
    /// <remarks>
    /// Connecting is anonymous on purpose: every visitor of the public shop needs
    /// to receive these notifications. The methods that *trigger* a broadcast are
    /// restricted, since otherwise any connected browser could tell every other
    /// browser to reload.
    /// </remarks>
    public class PromotionsHub : Hub
    {
        [Authorize(Roles = Roles.Admin)]
        public async Task NotifyPromotionsChanged()
        {
            await Clients.All.SendAsync("PromotionsChanged");
        }

        [Authorize(Roles = Roles.Admin)]
        public async Task NotifyActivePromotionsChanged()
        {
            await Clients.All.SendAsync("ActivePromotionsChanged");
        }
    }
}
