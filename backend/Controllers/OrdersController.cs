using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Storefront.Api.DTOs.Analytics;
using Storefront.Api.Services;

namespace Storefront.Api.Controllers
{
    /// <summary>
    /// Records what was ordered, never who ordered it.
    /// </summary>
    /// <remarks>
    /// Public, because checkout requires no account. The request DTO has no field
    /// for a name, phone, national ID, postcode or street — the checkout form still
    /// collects those, but they stay in the visitor's browser and in the WhatsApp
    /// message, so they cannot reach the database even by mistake.
    /// </remarks>
    [ApiController]
    [Route("api/v1/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orders;

        public OrdersController(IOrderService orders) => _orders = orders;

        [HttpPost]
        [AllowAnonymous]
        [EnableRateLimiting("orders")]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto, CancellationToken ct)
        {
            var result = await _orders.CreateAsync(dto, ct);

            return result.Succeeded ? Ok(result.Order) : BadRequest(result.Error);
        }
    }
}
