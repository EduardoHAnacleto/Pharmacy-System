using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Storefront.Api.DTOs.ItemPromotion;
using Storefront.Api.Models;
using Storefront.Api.Services;

namespace Storefront.Api.Controllers
{
    /// <summary>
    /// Category maintenance.
    /// </summary>
    /// <remarks>
    /// The listing stays where the storefront already reads it
    /// (<c>GET /api/v1/item-promotions/categories/all</c>); this controller adds the
    /// writes, so a shop that is not a pharmacy can define its own categories
    /// instead of inheriting five seeded pharmacy ones.
    /// </remarks>
    [ApiController]
    [Route("api/v1/categories")]
    [Authorize(Roles = Roles.Admin)]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categories;
        private readonly IPromotionService _promotions;

        public CategoriesController(ICategoryService categories, IPromotionService promotions)
        {
            _categories = categories;
            _promotions = promotions;
        }

        private int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private string CurrentUserName => User.Identity?.Name ?? "unknown";

        /// <summary>Lists categories for the admin screen.</summary>
        [HttpGet]
        public async Task<ActionResult<List<CategoryDto>>> List(CancellationToken ct) =>
            Ok(await _promotions.GetCategoriesAsync(ct));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CategoryWriteDto dto, CancellationToken ct)
        {
            var result = await _categories.CreateAsync(dto, CurrentUserId, CurrentUserName, ct);

            return result.Succeeded
                ? CreatedAtAction(nameof(List), new { id = result.Category!.Id }, result.Category)
                : Resolve(result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Rename(
            int id, [FromBody] CategoryWriteDto dto, CancellationToken ct)
        {
            var result = await _categories.RenameAsync(id, dto, CurrentUserId, CurrentUserName, ct);

            return result.Succeeded ? Ok(result.Category) : Resolve(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var result = await _categories.DeleteAsync(id, CurrentUserId, CurrentUserName, ct);

            return result.Succeeded ? NoContent() : Resolve(result);
        }

        private IActionResult Resolve(CategoryResult result) =>
            result.NotFound ? NotFound() : BadRequest(result.Error);
    }
}
