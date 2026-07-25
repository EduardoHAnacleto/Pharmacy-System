using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.DTOs.Store;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Services
{
    public interface IStoreSettingsService
    {
        Task<StoreSettingsDto> GetAsync(CancellationToken ct = default);

        Task<StoreSettingsDto> UpdateAsync(
            StoreSettingsUpdateDto request, int userId, string userName, CancellationToken ct = default);

        /// <summary>
        /// The settings the backend itself needs, read without the DTO round-trip.
        /// </summary>
        Task<StoreSettings> GetEntityAsync(CancellationToken ct = default);
    }

    /// <summary>
    /// Reads and writes the single row that configures the shop.
    /// </summary>
    /// <remarks>
    /// Cached, because the storefront asks for it on every page load and it changes
    /// perhaps once a month. Writes invalidate the scope, so an operator sees their
    /// own change immediately rather than waiting out a TTL and assuming the save
    /// failed.
    /// </remarks>
    public class StoreSettingsService : IStoreSettingsService
    {
        private const string CacheScope = "store-settings";
        private const int SingletonId = 1;

        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

        private readonly AppDbContext _context;
        private readonly RedisService _cache;
        private readonly IAuditLogger _audit;
        private readonly ILogger<StoreSettingsService> _logger;

        public StoreSettingsService(
            AppDbContext context,
            RedisService cache,
            IAuditLogger audit,
            ILogger<StoreSettingsService> logger)
        {
            _context = context;
            _cache = cache;
            _audit = audit;
            _logger = logger;
        }

        // ===============================
        // READ
        // ===============================
        public Task<StoreSettingsDto> GetAsync(CancellationToken ct = default) =>
            _cache.GetOrSetAsync(CacheScope, "current", CacheTtl, async () =>
                ToDto(await LoadAsync(ct)));

        public Task<StoreSettings> GetEntityAsync(CancellationToken ct = default) => LoadAsync(ct);

        /// <summary>
        /// Loads the row, falling back to defaults when it is absent.
        /// </summary>
        /// <remarks>
        /// The seeder creates it, but a database restored from a dump taken before
        /// this table existed would not have it. Returning an unsaved instance keeps
        /// the storefront rendering with the built-in defaults instead of failing.
        /// </remarks>
        private async Task<StoreSettings> LoadAsync(CancellationToken ct)
        {
            var settings = await _context.StoreSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == SingletonId, ct);

            if (settings != null)
                return settings;

            _logger.LogWarning("store_settings has no row; serving built-in defaults.");

            return new StoreSettings();
        }

        // ===============================
        // WRITE
        // ===============================
        public async Task<StoreSettingsDto> UpdateAsync(
            StoreSettingsUpdateDto request, int userId, string userName, CancellationToken ct = default)
        {
            var settings = await _context.StoreSettings
                .FirstOrDefaultAsync(s => s.Id == SingletonId, ct);

            if (settings == null)
            {
                settings = new StoreSettings { Id = SingletonId };
                _context.StoreSettings.Add(settings);
            }

            settings.StoreName = request.StoreName.Trim();
            settings.Tagline = Clean(request.Tagline);
            settings.LogoUrl = Clean(request.LogoUrl);
            settings.PrimaryColor = request.PrimaryColor.Trim();
            settings.SecondaryColor = request.SecondaryColor.Trim();

            settings.Address = Clean(request.Address);
            settings.City = Clean(request.City);
            settings.CountryCode = request.CountryCode.Trim().ToUpperInvariant();
            settings.TimeZone = request.TimeZone.Trim();
            settings.MapQuery = Clean(request.MapQuery);

            settings.Phone = Clean(request.Phone);
            settings.WhatsAppNumber = Clean(request.WhatsAppNumber);
            settings.Email = Clean(request.Email);
            settings.InstagramUrl = Clean(request.InstagramUrl);
            settings.FacebookUrl = Clean(request.FacebookUrl);

            settings.Currency = request.Currency.Trim().ToUpperInvariant();
            settings.Locale = request.Locale.Trim();
            settings.DeliveryEnabled = request.DeliveryEnabled;
            settings.PickupEnabled = request.PickupEnabled;
            settings.DeliveryFee = request.DeliveryFee;
            settings.MinDeliveryTotal = request.MinDeliveryTotal;
            settings.DeliveryCities = JoinCities(request.DeliveryCities);

            settings.CollectTaxId = request.CollectTaxId;
            settings.CollectPostalCode = request.CollectPostalCode;

            settings.OpeningHours = SerializeHours(request.OpeningHours);
            settings.FooterText = Clean(request.FooterText);
            settings.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            await _cache.InvalidateScopeAsync(CacheScope);

            await _audit.RecordAsync(
                userId,
                userName,
                "UpdateStoreSettings",
                nameof(StoreSettings),
                settings.Id,
                $"Loja \"{settings.StoreName}\", moeda {settings.Currency}, idioma {settings.Locale}",
                ct);

            return ToDto(settings);
        }

        // ===============================
        // VALIDATION HELPERS
        // ===============================

        /// <summary>
        /// Checks the values the model annotations cannot: that the time zone and
        /// currency actually exist on this machine, and that each opening range is
        /// ordered.
        /// </summary>
        /// <returns>An error message, or null when the request is coherent.</returns>
        public static string? Validate(StoreSettingsUpdateDto request)
        {
            if (!TimeZoneExists(request.TimeZone))
                return $"Fuso horário desconhecido: {request.TimeZone}.";

            if (!request.DeliveryEnabled && !request.PickupEnabled)
                return "Habilite ao menos entrega ou retirada.";

            foreach (var (day, ranges) in request.OpeningHours)
            {
                if (!int.TryParse(day, out var weekday) || weekday is < 1 or > 7)
                    return $"Dia da semana inválido: {day}. Use 1 (segunda) a 7 (domingo).";

                foreach (var range in ranges)
                {
                    if (string.CompareOrdinal(range.Open, range.Close) >= 0)
                        return $"O horário de fechamento deve ser depois da abertura ({range.Open}–{range.Close}).";
                }
            }

            return null;
        }

        /// <summary>
        /// Accepts both IANA and Windows ids, because the same configuration has to
        /// work whether the API runs in the Linux container or on a developer's
        /// Windows machine.
        /// </summary>
        private static bool TimeZoneExists(string id)
        {
            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return false;
            }
        }

        // ===============================
        // MAPPING
        // ===============================
        private static StoreSettingsDto ToDto(StoreSettings s) => new()
        {
            StoreName = s.StoreName,
            Tagline = s.Tagline,
            LogoUrl = s.LogoUrl,
            PrimaryColor = s.PrimaryColor,
            SecondaryColor = s.SecondaryColor,
            Address = s.Address,
            City = s.City,
            CountryCode = s.CountryCode,
            TimeZone = s.TimeZone,
            MapQuery = s.MapQuery,
            Phone = s.Phone,
            WhatsAppNumber = s.WhatsAppNumber,
            Email = s.Email,
            InstagramUrl = s.InstagramUrl,
            FacebookUrl = s.FacebookUrl,
            Currency = s.Currency,
            Locale = s.Locale,
            DeliveryEnabled = s.DeliveryEnabled,
            PickupEnabled = s.PickupEnabled,
            DeliveryFee = s.DeliveryFee,
            MinDeliveryTotal = s.MinDeliveryTotal,
            DeliveryCities = SplitCities(s.DeliveryCities),
            CollectTaxId = s.CollectTaxId,
            CollectPostalCode = s.CollectPostalCode,
            OpeningHours = DeserializeHours(s.OpeningHours),
            FooterText = s.FooterText,
        };

        private static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? JoinCities(IEnumerable<string> cities)
        {
            var cleaned = cities
                .Select(c => c.Trim())
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return cleaned.Length == 0 ? null : string.Join(',', cleaned);
        }

        private static IReadOnlyList<string> SplitCities(string? stored) =>
            string.IsNullOrWhiteSpace(stored)
                ? []
                : stored.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        private static string? SerializeHours(Dictionary<string, List<OpeningRangeDto>> hours) =>
            hours.Count == 0 ? null : JsonSerializer.Serialize(hours);

        /// <summary>
        /// Reads the stored JSON, treating anything unparseable as "no hours set".
        /// </summary>
        /// <remarks>
        /// A hand-edited row must not be able to make the public settings endpoint —
        /// and with it the whole storefront — throw. Losing the opening hours is a
        /// far smaller failure than losing the page.
        /// </remarks>
        private static IReadOnlyDictionary<string, List<OpeningRangeDto>> DeserializeHours(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, List<OpeningRangeDto>>();

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, List<OpeningRangeDto>>>(json)
                    ?? new Dictionary<string, List<OpeningRangeDto>>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, List<OpeningRangeDto>>();
            }
        }
    }
}
