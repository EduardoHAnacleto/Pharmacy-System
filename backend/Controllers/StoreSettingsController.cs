using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Api.DTOs.Store;
using Storefront.Api.Models;
using Storefront.Api.Services;

namespace Storefront.Api.Controllers
{
    /// <summary>
    /// The shop's own configuration: branding, contact, commerce rules, hours.
    /// </summary>
    /// <remarks>
    /// Reading is public because the storefront needs it before it can render its
    /// header — and everything in it is printed on the page anyway. Writing is
    /// Admin-only and audited.
    /// <para>
    /// This endpoint is what makes the project sellable to a second shop: nothing
    /// here requires a code change or a redeploy.
    /// </para>
    /// </remarks>
    [ApiController]
    [Route("api/v1/store-settings")]
    public class StoreSettingsController : ControllerBase
    {
        private readonly IStoreSettingsService _settings;

        public StoreSettingsController(IStoreSettingsService settings) => _settings = settings;

        private int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private string CurrentUserName => User.Identity?.Name ?? "unknown";

        /// <summary>Returns the shop's public configuration.</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<StoreSettingsDto>> Get(CancellationToken ct) =>
            Ok(await _settings.GetAsync(ct));

        /// <summary>Replaces the shop's configuration.</summary>
        [HttpPut]
        [Authorize(Roles = Roles.Admin)]
        public async Task<ActionResult<StoreSettingsDto>> Update(
            [FromBody] StoreSettingsUpdateDto dto, CancellationToken ct)
        {
            // Annotations cover shape; this covers coherence — an unknown time zone,
            // a shop with neither delivery nor pickup, an inverted opening range.
            var error = StoreSettingsService.Validate(dto);

            if (error != null)
                return BadRequest(error);

            return Ok(await _settings.UpdateAsync(dto, CurrentUserId, CurrentUserName, ct));
        }
    }
}
