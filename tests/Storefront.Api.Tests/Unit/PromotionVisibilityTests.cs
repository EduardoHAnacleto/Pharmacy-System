using Storefront.Api.Models;
using Xunit;

namespace Storefront.Api.Tests.Unit;

public class PromotionVisibilityTests
{
    private static readonly DateTime Now = new(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    private static ItemPromotion WithStatus(string status) => new()
    {
        Name = "Promo",
        ImagePath = "/images/promotions/a.png",
        ProductType = "default",
        CreatedByUserName = "admin",
        Status = status,
        DateStart = Now.AddDays(-1),
        DateEnd = Now.AddDays(1),
    };

    [Theory]
    [InlineData(PromotionStatus.Active)]
    [InlineData(PromotionStatus.Scheduled)]
    [InlineData(PromotionStatus.Expired)]
    public void InsideItsWindow_PublishableStatusesAreVisible(string status)
    {
        // Visibility is deliberately not "status == Active": that would make a
        // promotion invisible until the maintenance job happened to run, so the
        // window is evaluated per request and status only decides eligibility.
        Assert.True(WithStatus(status).IsVisibleAt(Now));
    }

    [Theory]
    [InlineData(PromotionStatus.Draft)]
    [InlineData(PromotionStatus.Archived)]
    public void DraftAndArchivedAreNeverVisible(string status) =>
        Assert.False(WithStatus(status).IsVisibleAt(Now));

    [Fact]
    public void AnArchivedPromotionStaysHiddenEvenInsideItsWindow()
    {
        // The point of archiving: the row and its image survive, the storefront
        // does not show it.
        var archived = WithStatus(PromotionStatus.Archived);

        Assert.False(archived.IsVisibleAt(Now));
        Assert.Equal("/images/promotions/a.png", archived.ImagePath);
    }

    [Fact]
    public void BeforeTheWindowOpens_ItIsNotVisible()
    {
        var promotion = WithStatus(PromotionStatus.Active);
        promotion.DateStart = Now.AddDays(1);
        promotion.DateEnd = Now.AddDays(2);

        Assert.False(promotion.IsVisibleAt(Now));
    }

    [Fact]
    public void AfterTheWindowCloses_ItIsNotVisible()
    {
        var promotion = WithStatus(PromotionStatus.Active);
        promotion.DateStart = Now.AddDays(-3);
        promotion.DateEnd = Now.AddDays(-1);

        Assert.False(promotion.IsVisibleAt(Now));
    }

    [Fact]
    public void TheWindowBoundsAreInclusive()
    {
        var promotion = WithStatus(PromotionStatus.Active);
        promotion.DateStart = Now;
        promotion.DateEnd = Now;

        Assert.True(promotion.IsVisibleAt(Now));
    }

    [Fact]
    public void StatusIsValid_RejectsAnythingUnknown()
    {
        Assert.True(PromotionStatus.IsValid(PromotionStatus.Archived));
        Assert.False(PromotionStatus.IsValid("Deleted"));
        Assert.False(PromotionStatus.IsValid("active"));
        Assert.False(PromotionStatus.IsValid(""));
    }

    [Fact]
    public void ANewPromotionDefaultsToDraft()
    {
        // Nothing reaches the storefront by accident.
        Assert.Equal(PromotionStatus.Draft, new ItemPromotion().Status);
    }
}
