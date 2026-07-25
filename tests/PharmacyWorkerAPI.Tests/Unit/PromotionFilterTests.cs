using PharmacyWorkerAPI.DTOs.ItemPromotion;
using Xunit;

namespace PharmacyWorkerAPI.Tests.Unit;

/// <summary>
/// Filter normalisation, which decides both what is queried and how it is cached.
/// </summary>
/// <remarks>
/// The cache key matters as much as the query: two requests that mean the same
/// thing must produce one entry, and two that mean different things must never
/// share one.
/// </remarks>
public class PromotionFilterTests
{
    [Fact]
    public void Normalised_TrimsAndDropsBlankSearch()
    {
        var filter = new PromotionFilterDto { Search = "   " }.Normalised();

        Assert.Null(filter.Search);
    }

    [Fact]
    public void Normalised_TrimsSurroundingWhitespace()
    {
        var filter = new PromotionFilterDto { Search = "  dipirona  " }.Normalised();

        Assert.Equal("dipirona", filter.Search);
    }

    [Fact]
    public void Normalised_CapsSearchAtTheColumnLength()
    {
        // Longer than the name column can hold, so it could only ever match nothing
        // — and each attempt would otherwise become its own cache entry.
        var filter = new PromotionFilterDto { Search = new string('x', 500) }.Normalised();

        Assert.Equal(100, filter.Search!.Length);
    }

    [Fact]
    public void Normalised_SwapsAnInvertedPriceRange()
    {
        var filter = new PromotionFilterDto { MinPrice = 50m, MaxPrice = 10m }.Normalised();

        // A range nobody could satisfy is more likely a typo than an intent to
        // return nothing.
        Assert.Equal(10m, filter.MinPrice);
        Assert.Equal(50m, filter.MaxPrice);
    }

    [Fact]
    public void Normalised_DiscardsNegativePrices()
    {
        var filter = new PromotionFilterDto { MinPrice = -5m, MaxPrice = -1m }.Normalised();

        Assert.Null(filter.MinPrice);
        Assert.Null(filter.MaxPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Normalised_DiscardsANonPositiveCategoryId(int categoryId)
    {
        var filter = new PromotionFilterDto { CategoryId = categoryId }.Normalised();

        Assert.Null(filter.CategoryId);
    }

    [Fact]
    public void Normalised_DiscardsAnUnrecognisedSort()
    {
        var filter = new PromotionFilterDto { Sort = "'; DROP TABLE" }.Normalised();

        Assert.Null(filter.Sort);
    }

    [Theory]
    [InlineData(PromotionSort.EndingSoon)]
    [InlineData(PromotionSort.PriceAscending)]
    [InlineData(PromotionSort.PriceDescending)]
    [InlineData(PromotionSort.Newest)]
    [InlineData(PromotionSort.Name)]
    public void Normalised_KeepsEveryRecognisedSort(string sort)
    {
        var filter = new PromotionFilterDto { Sort = sort }.Normalised();

        Assert.Equal(sort, filter.Sort);
    }

    [Fact]
    public void CacheKey_IsStableForEquivalentFilters()
    {
        var padded = new PromotionFilterDto { Search = " leite ", CategoryId = 2 }.Normalised();
        var clean = new PromotionFilterDto { Search = "leite", CategoryId = 2 }.Normalised();

        Assert.Equal(clean.CacheKey(), padded.CacheKey());
    }

    [Fact]
    public void CacheKey_DiffersWhenAnyFilterDiffers()
    {
        var baseline = new PromotionFilterDto().Normalised().CacheKey();

        var keys = new[]
        {
            baseline,
            new PromotionFilterDto { Search = "leite" }.Normalised().CacheKey(),
            new PromotionFilterDto { CategoryId = 1 }.Normalised().CacheKey(),
            new PromotionFilterDto { MinPrice = 5m }.Normalised().CacheKey(),
            new PromotionFilterDto { MaxPrice = 5m }.Normalised().CacheKey(),
            new PromotionFilterDto { Sort = PromotionSort.Name }.Normalised().CacheKey(),
        };

        // Any collision here would serve one visitor's filtered grid to another.
        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void CacheKey_UsesTheInvariantDecimalSeparator()
    {
        var filter = new PromotionFilterDto { MinPrice = 9.9m }.Normalised();

        // A comma here on a pt-BR host and a dot on an en-US one would key the same
        // filter twice.
        Assert.Contains("9.9", filter.CacheKey(), StringComparison.Ordinal);
    }
}
