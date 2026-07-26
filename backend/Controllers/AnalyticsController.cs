using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Storefront.Api.DTOs.Analytics;
using Storefront.Api.Models;
using Storefront.Api.Services;

namespace Storefront.Api.Controllers
{
    /// <summary>
    /// Storefront telemetry and the reports built from it.
    /// </summary>
    /// <remarks>
    /// Ingestion is public because the visitors being measured are anonymous; the
    /// reports are Admin-only. Nothing here stores or accepts personal data.
    /// </remarks>
    [ApiController]
    [Route("api/v1/analytics")]
    public class AnalyticsController : ControllerBase
    {
        private readonly IAnalyticsService _analytics;

        public AnalyticsController(IAnalyticsService analytics) => _analytics = analytics;

        // ===============================
        // INGESTION
        // ===============================

        /// <summary>
        /// Records a batch of storefront events.
        /// </summary>
        [HttpPost("events")]
        [AllowAnonymous]
        [EnableRateLimiting("events")]
        public async Task<IActionResult> Track(
            [FromBody] TrackEventBatchDto batch, CancellationToken ct)
        {
            var accepted = await _analytics.TrackAsync(batch, ct);

            // Accepted, not Created: there is no resource for the caller to fetch,
            // and the count tells a client how many of its events were known types.
            return Accepted(new { accepted });
        }

        // ===============================
        // REPORTS
        // ===============================

        [HttpGet("funnel")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetFunnel(
            CancellationToken ct,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null)
        {
            var (start, end) = Range(from, to);

            return Ok(await _analytics.GetFunnelAsync(start, end, ct));
        }

        [HttpGet("promotions")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetPromotionPerformance(
            CancellationToken ct,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null,
            [FromQuery] int limit = 20)
        {
            var (start, end) = Range(from, to);

            if (limit is <= 0 or > 200) limit = 20;

            return Ok(await _analytics.GetPromotionPerformanceAsync(start, end, limit, ct));
        }

        [HttpGet("timeseries")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetTimeSeries(
            CancellationToken ct,
            [FromQuery] string eventType = AnalyticsEventType.PromotionView,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null)
        {
            if (!AnalyticsEventType.IsValid(eventType))
                return BadRequest("Tipo de evento inválido.");

            var (start, end) = Range(from, to);

            return Ok(await _analytics.GetTimeSeriesAsync(eventType, start, end, ct));
        }

        [HttpGet("sales")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetSales(
            CancellationToken ct,
            [FromQuery] DateOnly? from = null,
            [FromQuery] DateOnly? to = null)
        {
            var (start, end) = Range(from, to);

            return Ok(await _analytics.GetSalesSummaryAsync(start, end, ct));
        }

        [HttpGet("alerts")]
        [Authorize(Roles = Roles.Admin)]
        public async Task<IActionResult> GetAlerts(CancellationToken ct) =>
            Ok(await _analytics.GetAlertsAsync(ct));

        /// <summary>Defaults to the last 30 days, and never lets the range invert.</summary>
        private static (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var start = from ?? today.AddDays(-30);
            var end = to ?? today;

            return start > end ? (end, start) : (start, end);
        }
    }
}
