using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.DTOs.ItemPromotion;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Services
{
    /// <summary>Outcome of a category write.</summary>
    public record CategoryResult(
        bool Succeeded,
        CategoryDto? Category = null,
        string? Error = null,
        bool NotFound = false)
    {
        public static CategoryResult Rejected(string error) => new(false, Error: error);

        public static CategoryResult Missing() => new(false, NotFound: true);
    }

    public interface ICategoryService
    {
        Task<CategoryResult> CreateAsync(
            CategoryWriteDto dto, int userId, string userName, CancellationToken ct = default);

        Task<CategoryResult> RenameAsync(
            int id, CategoryWriteDto dto, int userId, string userName, CancellationToken ct = default);

        Task<CategoryResult> DeleteAsync(
            int id, int userId, string userName, CancellationToken ct = default);
    }

    /// <summary>
    /// Category maintenance.
    /// </summary>
    /// <remarks>
    /// The five seeded categories are pharmacy-specific ("Genérico", "Similar"), so a
    /// bakery or a parts shop needs its own — which previously meant editing a SQL
    /// seed file and redeploying.
    /// </remarks>
    public class CategoryService : ICategoryService
    {
        private const string CategoryScope = "categories";
        private const string PromotionScope = "item-promotions";

        private readonly AppDbContext _context;
        private readonly RedisService _cache;
        private readonly IAuditLogger _audit;

        public CategoryService(AppDbContext context, RedisService cache, IAuditLogger audit)
        {
            _context = context;
            _cache = cache;
            _audit = audit;
        }

        // ===============================
        // CREATE
        // ===============================
        public async Task<CategoryResult> CreateAsync(
            CategoryWriteDto dto, int userId, string userName, CancellationToken ct = default)
        {
            var name = dto.Name.Trim();

            if (await ExistsAsync(name, null, ct))
                return CategoryResult.Rejected($"Já existe uma categoria chamada \"{name}\".");

            var category = new Category { Name = name, CreatedAt = DateTime.UtcNow };

            _context.Categories.Add(category);
            await _context.SaveChangesAsync(ct);

            await InvalidateAsync();

            await _audit.RecordAsync(
                userId, userName, "CreateCategory", nameof(Category), category.Id, name, ct);

            return new CategoryResult(true, ToDto(category));
        }

        // ===============================
        // RENAME
        // ===============================
        public async Task<CategoryResult> RenameAsync(
            int id, CategoryWriteDto dto, int userId, string userName, CancellationToken ct = default)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category == null)
                return CategoryResult.Missing();

            var name = dto.Name.Trim();

            if (await ExistsAsync(name, id, ct))
                return CategoryResult.Rejected($"Já existe uma categoria chamada \"{name}\".");

            var previous = category.Name;
            category.Name = name;

            await _context.SaveChangesAsync(ct);
            await InvalidateAsync();

            await _audit.RecordAsync(
                userId, userName, "RenameCategory", nameof(Category), id,
                $"{previous} → {name}", ct);

            return new CategoryResult(true, ToDto(category));
        }

        // ===============================
        // DELETE
        // ===============================

        /// <summary>
        /// Removes a category, but only while nothing references it.
        /// </summary>
        /// <remarks>
        /// Checked here rather than left to the FK so the caller gets a 400 that says
        /// how many promotions are in the way, instead of a 500 from a constraint
        /// violation. The FK is <c>Restrict</c> and remains the real guarantee.
        /// </remarks>
        public async Task<CategoryResult> DeleteAsync(
            int id, int userId, string userName, CancellationToken ct = default)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

            if (category == null)
                return CategoryResult.Missing();

            var inUse = await _context.ItemPromotions.CountAsync(p => p.CategoryId == id, ct);

            if (inUse > 0)
            {
                return CategoryResult.Rejected(
                    $"A categoria \"{category.Name}\" está em uso por {inUse} promoção(ões). "
                    + "Mova-as para outra categoria antes de excluir.");
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync(ct);

            await InvalidateAsync();

            await _audit.RecordAsync(
                userId, userName, "DeleteCategory", nameof(Category), id, category.Name, ct);

            return new CategoryResult(true, ToDto(category));
        }

        // ===============================
        // HELPERS
        // ===============================

        /// <summary>Case-insensitive duplicate check, excluding one id when renaming.</summary>
        private Task<bool> ExistsAsync(string name, int? excludeId, CancellationToken ct) =>
            _context.Categories.AnyAsync(
                c => c.Name.ToLower() == name.ToLower() && (excludeId == null || c.Id != excludeId),
                ct);

        /// <summary>
        /// Both scopes: a renamed category changes the category list and every cached
        /// promotion page that carries its name.
        /// </summary>
        private async Task InvalidateAsync()
        {
            await _cache.InvalidateScopeAsync(CategoryScope);
            await _cache.InvalidateScopeAsync(PromotionScope);
        }

        private static CategoryDto ToDto(Category category) =>
            new() { Id = category.Id, Name = category.Name };
    }
}
