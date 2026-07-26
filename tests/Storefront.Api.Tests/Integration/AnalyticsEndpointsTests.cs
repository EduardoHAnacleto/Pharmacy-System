using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Storefront.Api.DTOs.Analytics;
using Storefront.Api.DTOs.ItemPromotion;
using Storefront.Api.Models;
using Xunit;

namespace Storefront.Api.Tests.Integration;

/// <summary>
/// The reports, read end to end against a real database.
/// </summary>
/// <remarks>
/// These exist because of a defect they would have caught. The funnel and the
/// per-promotion views read only <c>analytics_daily</c>, which
/// <c>RollUpAnalyticsAsync</c> populates for completed days only — while sales and
/// order counts came straight from <c>orders</c> and were live. Nothing was wrong
/// with either half in isolation, and both halves had tests. What was missing was
/// the assertion that spans them: an event recorded now has to appear in a report
/// for a range that includes now.
/// <para>
/// Assertions are deltas rather than absolutes wherever a count is global: tests in
/// this collection share one database and create orders and promotions of their own,
/// so a fixed expected total would be a test that fails whenever a sibling changes.
/// </para>
/// </remarks>
[Collection(ApiCollection.Name)]
public class AnalyticsEndpointsTests
{
    private readonly ApiFixture _fixture;

    public AnalyticsEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    private static string Today =>
        DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A range that starts and ends today, so only today's data can satisfy it.</summary>
    private static string TodayRange => $"from={Today}&to={Today}";

    // ===============================
    // THE REGRESSION
    // ===============================

    [SkippableFact]
    public async Task Funnel_CountsAnEventRecordedToday()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var before = await FunnelAsync(client);

        // Two fresh sessions, so the step counts are distinguishable from a single
        // visit and cannot collide with a session another test already spent today.
        var visitor = NewSession();
        var other = NewSession();

        await TrackAsync(
            (AnalyticsEventType.PromotionView, null, visitor),
            (AnalyticsEventType.PromotionView, null, other),
            (AnalyticsEventType.AddToCart, null, visitor),
            (AnalyticsEventType.CheckoutStarted, null, visitor),
            (AnalyticsEventType.WhatsAppClick, null, visitor));

        var after = await FunnelAsync(client);

        // Before the fix every one of these was zero: the rollup skips today, and the
        // funnel read nothing else.
        Assert.Equal(before.PromotionViews + 2, after.PromotionViews);
        Assert.Equal(before.AddToCart + 1, after.AddToCart);
        Assert.Equal(before.CheckoutStarted + 1, after.CheckoutStarted);
        Assert.Equal(before.WhatsAppClicks + 1, after.WhatsAppClicks);
    }

    [SkippableFact]
    public async Task Funnel_ReportsARateOnceTheStepsAreNonZero()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        await TrackAsync(
            (AnalyticsEventType.PromotionView, null, NewSession()),
            (AnalyticsEventType.AddToCart, null, NewSession()));

        var funnel = await FunnelAsync(client);

        // A conversion rate of zero was previously indistinguishable from "no data",
        // which is the reading that made the dashboard look broken.
        Assert.True(funnel.PromotionViews > 0);
        Assert.True(funnel.ViewToCartRate > 0);
    }

    [SkippableFact]
    public async Task PromotionPerformance_CountsTodaysViewsAgainstTodaysSales()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var promotionId = await CreatePromotionAsync(client, "Desempenho de hoje");

        var visitor = NewSession();

        await TrackAsync(
            (AnalyticsEventType.PromotionView, promotionId, visitor),
            (AnalyticsEventType.PromotionView, promotionId, NewSession()),
            (AnalyticsEventType.AddToCart, promotionId, visitor));

        var ordered = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            fulfillmentType = "pickup",
            paymentMethod = "pix",
            deliveryFee = 0,
            items = new[]
            {
                new { promotionId, name = "Desempenho de hoje", unitPrice = 9.90m, quantity = 2 },
            },
        });

        ordered.EnsureSuccessStatusCode();

        var rows = await client.GetFromJsonAsync<List<PromotionPerformanceDto>>(
            $"/api/v1/analytics/promotions?{TodayRange}&limit=200");

        var row = Assert.Single(rows!, r => r.PromotionId == promotionId);

        // The inconsistency this test pins down: 2 units sold reported beside 0 views,
        // because the two numbers came from sources with different cut-off points.
        Assert.Equal(2, row.Views);
        Assert.Equal(1, row.AddToCart);
        Assert.Equal(2, row.OrderedQuantity);
        Assert.True(row.ConversionRate > 0);
    }

    [SkippableFact]
    public async Task TimeSeries_HasAPointForToday()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        await TrackAsync((AnalyticsEventType.PromotionView, null, NewSession()));

        var points = await client.GetFromJsonAsync<List<DailyPointDto>>(
            $"/api/v1/analytics/timeseries?eventType={AnalyticsEventType.PromotionView}&{TodayRange}");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var point = Assert.Single(points!, p => p.Date == today);
        Assert.True(point.Count > 0);
    }

    [SkippableFact]
    public async Task Funnel_CountsEachEventOnceOverAWideRange()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        // A range wide enough to cover both sources: the rollup owns the days before
        // today, raw owns today. Raw rows survive the whole retention window, so if
        // the split were anywhere other than exactly today, today would be read from
        // both and counted twice.
        var wide = $"from={DateTime.UtcNow.AddDays(-30):yyyy-MM-dd}&to={Today}";

        var before = await FunnelAsync(client, wide);

        // Fresh sessions: the funnel counts distinct sessions, so reusing a key another
        // test already spent today would add nothing and make this assert order.
        await TrackAsync(
            (AnalyticsEventType.PromotionView, null, NewSession()),
            (AnalyticsEventType.PromotionView, null, NewSession()));

        var after = await FunnelAsync(client, wide);

        // Exactly two, not four.
        Assert.Equal(before.PromotionViews + 2, after.PromotionViews);

        // And the same question asked twice gives the same answer.
        var again = await FunnelAsync(client, wide);
        Assert.Equal(after.PromotionViews, again.PromotionViews);
    }

    // ===============================
    // INGESTION
    // ===============================

    [SkippableFact]
    public async Task Track_IsPublic()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        // The visitors being measured are anonymous; requiring a token would mean
        // measuring nothing.
        var response = await client.PostAsJsonAsync("/api/v1/analytics/events", new
        {
            events = new[]
            {
                new { eventType = AnalyticsEventType.PromotionView, sessionKey = NewSession() },
            },
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [SkippableFact]
    public async Task Track_DropsUnknownEventTypes()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/analytics/events", new
        {
            events = new[]
            {
                new { eventType = "not_a_real_event", sessionKey = NewSession() },
                new { eventType = AnalyticsEventType.PromotionView, sessionKey = NewSession() },
            },
        });

        var accepted = await response.Content.ReadFromJsonAsync<AcceptedCount>();

        // This endpoint is public, so an open-ended type column becomes a junk drawer.
        Assert.Equal(1, accepted!.Accepted);
    }

    [SkippableFact]
    public async Task Reports_RequireAnAdminToken()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        foreach (var path in new[] { "funnel", "promotions", "sales", "timeseries", "alerts" })
        {
            var response = await client.GetAsync($"/api/v1/analytics/{path}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [SkippableFact]
    public async Task TimeSeries_RejectsAnUnknownEventType()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/analytics/timeseries?eventType=nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ===============================
    // SALES
    // ===============================

    [SkippableFact]
    public async Task Sales_CountsAnOrderPlacedToday()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var before = await client.GetFromJsonAsync<SalesSummaryDto>(
            $"/api/v1/analytics/sales?{TodayRange}");

        var order = await client.PostAsJsonAsync("/api/v1/orders", new
        {
            fulfillmentType = "delivery",
            paymentMethod = "pix",
            deliveryCity = "Santa Terezinha de Itaipu",
            deliveryFee = 8.00m,
            items = new[]
            {
                new { promotionId = (int?)null, name = "Item solto", unitPrice = 10.00m, quantity = 1 },
            },
        });

        order.EnsureSuccessStatusCode();

        var after = await client.GetFromJsonAsync<SalesSummaryDto>(
            $"/api/v1/analytics/sales?{TodayRange}");

        Assert.Equal(before!.Orders + 1, after!.Orders);
        Assert.Equal(before.DeliveryOrders + 1, after.DeliveryOrders);
        Assert.True(after.Revenue > before.Revenue);
    }

    // ===============================
    // HELPERS
    // ===============================

    private sealed record AcceptedCount(int Accepted);

    /// <summary>A 32-character key, matching the char(32) column.</summary>
    /// <remarks>
    /// Every test mints its own. The funnel counts <em>distinct sessions</em>, so two
    /// tests sharing a session key on the same day do not each add to it — a delta
    /// assertion then depends on which test ran first, which is exactly how the
    /// wide-range test failed while the narrow one passed.
    /// </remarks>
    private static string NewSession() => Guid.NewGuid().ToString("N");

    private async Task<FunnelDto> FunnelAsync(HttpClient client, string? range = null) =>
        (await client.GetFromJsonAsync<FunnelDto>(
            $"/api/v1/analytics/funnel?{range ?? TodayRange}"))!;

    private async Task TrackAsync(params (string Type, int? PromotionId, string Session)[] events)
    {
        using var client = _fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/analytics/events", new
        {
            events = events.Select(e => new
            {
                eventType = e.Type,
                promotionId = e.PromotionId,
                sessionKey = e.Session,
            }),
        });

        response.EnsureSuccessStatusCode();
    }

    private static async Task<int> CreatePromotionAsync(HttpClient client, string name)
    {
        var png = new ByteArrayContent(
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        ]);

        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var form = new MultipartFormDataContent
        {
            { new StringContent(name), "Name" },
            { new StringContent("9.90"), "Price" },
            { new StringContent("19.90"), "PriceBefore" },
            { new StringContent("2026-01-01T00:00:00Z"), "DateStart" },
            { new StringContent("2099-12-31T00:00:00Z"), "DateEnd" },
            { new StringContent("true"), "IsActive" },
            { new StringContent("1"), "CategoryId" },
            { new StringContent("default"), "ProductType" },
        };

        form.Add(png, "Image", "promo.png");

        var created = await client.PostAsync("/api/v1/item-promotions", form);
        created.EnsureSuccessStatusCode();

        var dto = await created.Content.ReadFromJsonAsync<ItemPromotionResponseDto>();

        return dto!.Id;
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _fixture.CreateClient();
        var token = await LoginHelper.GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}
