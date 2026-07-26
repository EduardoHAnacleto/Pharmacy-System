using System.Globalization;

namespace Storefront.Api.DTOs.ItemPromotion
{
    /// <summary>Sort orders the storefront grid offers.</summary>
    public static class PromotionSort
    {
        public const string EndingSoon = "endingSoon";
        public const string PriceAscending = "priceAsc";
        public const string PriceDescending = "priceDesc";
        public const string Newest = "newest";
        public const string Name = "name";

        private static readonly string[] All =
            [EndingSoon, PriceAscending, PriceDescending, Newest, Name];

        public static bool IsValid(string? value) => value != null && All.Contains(value);
    }

    /// <summary>
    /// The storefront's search and filter parameters.
    /// </summary>
    /// <remarks>
    /// Bound from the query string. Values are normalised before use so that
    /// equivalent requests — different casing, padding, an inverted price range —
    /// produce one cache entry rather than several.
    /// </remarks>
    public class PromotionFilterDto
    {
        /// <summary>Free-text match against the promotion name.</summary>
        public string? Search { get; set; }

        public int? CategoryId { get; set; }

        public decimal? MinPrice { get; set; }

        public decimal? MaxPrice { get; set; }

        public string? Sort { get; set; }

        /// <summary>
        /// Returns a cleaned copy: trimmed search, non-negative prices in order,
        /// and a recognised sort.
        /// </summary>
        public PromotionFilterDto Normalised()
        {
            var search = Search?.Trim();

            // A search longer than the column it matches can only ever return
            // nothing, and would otherwise become its own cache entry per attempt.
            if (search is { Length: > 100 })
                search = search[..100];

            var min = Clamp(MinPrice);
            var max = Clamp(MaxPrice);

            if (min.HasValue && max.HasValue && min > max)
                (min, max) = (max, min);

            return new PromotionFilterDto
            {
                Search = string.IsNullOrWhiteSpace(search) ? null : search,
                CategoryId = CategoryId is > 0 ? CategoryId : null,
                MinPrice = min,
                MaxPrice = max,
                Sort = PromotionSort.IsValid(Sort) ? Sort : null,
            };
        }

        private static decimal? Clamp(decimal? value) =>
            value is null or < 0 ? null : value;

        /// <summary>A stable string identifying this filter for cache keying.</summary>
        public string CacheKey()
        {
            // InvariantCulture: a decimal rendered with a comma in one request and a
            // dot in another would key the same filter twice.
            var min = MinPrice?.ToString(CultureInfo.InvariantCulture) ?? "-";
            var max = MaxPrice?.ToString(CultureInfo.InvariantCulture) ?? "-";

            return $"q:{Search ?? "-"}|c:{CategoryId?.ToString(CultureInfo.InvariantCulture) ?? "-"}"
                + $"|p:{min}-{max}|s:{Sort ?? "-"}";
        }
    }
}
