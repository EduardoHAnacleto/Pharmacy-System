using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmacyWorkerAPI.DTOs.ItemPromotion;
using PharmacyWorkerAPI.Models;
using PharmacyWorkerAPI.Services;

namespace PharmacyWorkerAPI.Controllers
{
    /// <summary>
    /// Promotion endpoints. Reads are public — this is a storefront; writes require
    /// an Admin token.
    /// </summary>
    [ApiController]
    [Route("api/v1/item-promotions")]
    public class ItemPromotionController : ControllerBase
    {
        private readonly IPromotionService _promotions;

        public ItemPromotionController(IPromotionService promotions) => _promotions = promotions;

        // ===============================
        // CURRENT USER
        // ===============================
        private int CurrentUserId =>
            int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

        private string CurrentUserName => User.Identity?.Name ?? "unknown";

        // ===============================
        // CREATE PROMOTION
        // ===============================
        [HttpPost]
        [Authorize(Roles = Roles.Admin)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create(
            [FromForm] ItemPromotionCreateRequestDto dto, CancellationToken ct)
        {
            var result = await _promotions.CreateAsync(dto, CurrentUserId, CurrentUserName, ct);

            if (!result.Succeeded)
                return BadRequest(result.Error);

            return CreatedAtAction(nameof(GetById), new { id = result.Promotion!.Id }, result.Promotion);
        }

        // ===============================
        // GET BY ID
        // ===============================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var promotion = await _promotions.GetByIdAsync(id, ct);

            return promotion == null ? NotFound() : Ok(promotion);
        }

        // ===============================
        // DELETE PROMOTION
        // ===============================
        [HttpDelete("{id:int}")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var deleted = await _promotions.DeleteAsync(id, ct);

            return deleted ? NoContent() : NotFound();
        }

        // ===============================
        // GET ALL PROMOTIONS ORDERED BY END DATE
        // ===============================
        [HttpGet("all")]
        public async Task<IActionResult> GetAll(CancellationToken ct) =>
            Ok(await _promotions.GetAllAsync(ct));

        // ===============================
        // GET ACTIVE PROMOTIONS
        // ===============================
        [HttpGet("active/all")]
        public async Task<IActionResult> GetActive(CancellationToken ct) =>
            Ok(await _promotions.GetActiveAsync(ct));

        // ===============================
        // GET ALL PROMOTIONS
        // Filter by minimum CreatedAt
        // ===============================
        [HttpGet("created-after")]
        public async Task<IActionResult> GetAllCreatedAfter(
            [FromQuery] DateTime? minCreatedAt, CancellationToken ct) =>
            Ok(await _promotions.GetCreatedAfterAsync(minCreatedAt, ct));

        // ===============================
        // GET ACTIVE PROMOTIONS (PAGED)
        // ===============================
        [HttpGet("active")]
        public async Task<IActionResult> GetActivePaged(
            CancellationToken ct,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? timeZone = null) =>
            Ok(await _promotions.GetActivePagedAsync(page, pageSize, timeZone, ct));

        // ===============================
        // GET CATEGORIES
        // ===============================
        [HttpGet("categories/all")]
        public async Task<IActionResult> GetAllCategories(CancellationToken ct) =>
            Ok(await _promotions.GetCategoriesAsync(ct));
    }
}
