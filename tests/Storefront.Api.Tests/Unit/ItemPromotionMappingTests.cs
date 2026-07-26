using Storefront.Api.Mapping;
using Storefront.Api.Models;
using Xunit;

namespace Storefront.Api.Tests.Unit;

public class ItemPromotionMappingTests
{
    private static ItemPromotion Sample() => new()
    {
        Id = 7,
        Name = "Dipirona 500mg",
        Price = 9.90m,
        PriceBefore = 14.50m,
        ImagePath = "/images/promotions/abc.webp",
        DateStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        DateEnd = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
        Status = PromotionStatus.Active,
        CategoryId = 3,
        ProductType = "default",
        CreatedByUserId = 1,
        CreatedByUserName = "admin",
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
    };

    [Fact]
    public void ToDto_CopiesEveryField()
    {
        var promotion = Sample();

        var dto = promotion.ToDto();

        Assert.Equal(promotion.Id, dto.Id);
        Assert.Equal(promotion.Name, dto.Name);
        Assert.Equal(promotion.Price, dto.Price);
        Assert.Equal(promotion.PriceBefore, dto.PriceBefore);
        Assert.Equal(promotion.DateStart, dto.DateStart);
        Assert.Equal(promotion.DateEnd, dto.DateEnd);
        Assert.Equal(promotion.Status, dto.Status);
        Assert.Equal(promotion.CategoryId, dto.CategoryId);
        Assert.Equal(promotion.ProductType, dto.ProductType);
        Assert.Equal(promotion.CreatedByUserId, dto.CreatedByUserId);
        Assert.Equal(promotion.CreatedByUserName, dto.CreatedByUserName);
    }

    [Fact]
    public void ToDto_ReturnsTheImagePathUnchanged()
    {
        // Relative, not absolute: nginx serves /images from the same origin, and
        // the old PublicBaseUrl prefix made URLs unusable off the Docker host.
        var dto = Sample().ToDto();

        Assert.Equal("/images/promotions/abc.webp", dto.ImageUrl);
        Assert.StartsWith("/", dto.ImageUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCompiledAndQueryProjectionsAgree()
    {
        // The in-memory path and the EF path are the same expression, one compiled.
        // This is the property that stopped POST and GET from disagreeing.
        var promotion = Sample();

        var fromExpression = ItemPromotionMapping.ToResponseDto.Compile()(promotion);
        var fromExtension = promotion.ToDto();

        Assert.Equal(fromExpression.Id, fromExtension.Id);
        Assert.Equal(fromExpression.ImageUrl, fromExtension.ImageUrl);
        Assert.Equal(fromExpression.Name, fromExtension.Name);
        Assert.Equal(fromExpression.Price, fromExtension.Price);
    }
}
