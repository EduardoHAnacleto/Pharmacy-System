using Microsoft.EntityFrameworkCore;
using Storefront.Api.Data;
using Storefront.Api.DTOs.Analytics;
using Storefront.Api.Models;

namespace Storefront.Api.Services
{
    public interface IAnalyticsService
    {
        Task<int> TrackAsync(TrackEventBatchDto batch, CancellationToken ct = default);

        Task<FunnelDto> GetFunnelAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

        Task<List<PromotionPerformanceDto>> GetPromotionPerformanceAsync(
            DateOnly from, DateOnly to, int limit, CancellationToken ct = default);

        Task<List<DailyPointDto>> GetTimeSeriesAsync(
            string eventType, DateOnly from, DateOnly to, CancellationToken ct = default);

        Task<SalesSummaryDto> GetSalesSummaryAsync(
            DateOnly from, DateOnly to, CancellationToken ct = default);

        Task<List<OperationalAlertDto>> GetAlertsAsync(CancellationToken ct = default);
    }

    public class AnalyticsService : IAnalyticsService
    {
        private readonly AppDbContext _context;

        public AnalyticsService(AppDbContext context) => _context = context;

        // ===============================
        // INGESTION
        // ===============================
        public async Task<int> TrackAsync(TrackEventBatchDto batch, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            // Unknown event types are dropped rather than stored: this endpoint is
            // public, and an open-ended type column becomes a junk drawer.
            var events = batch.Events
                .Where(e => AnalyticsEventType.IsValid(e.EventType))
                .Select(e => new AnalyticsEvent
                {
                    EventType = e.EventType,
                    PromotionId = e.PromotionId,
                    SessionKey = string.IsNullOrWhiteSpace(e.SessionKey) ? null : e.SessionKey,
                    OccurredAt = now,
                })
                .ToList();

            if (events.Count == 0)
                return 0;

            _context.AnalyticsEvents.AddRange(events);
            await _context.SaveChangesAsync(ct);

            return events.Count;
        }

        // ===============================
        // FUNNEL
        // ===============================
        public async Task<FunnelDto> GetFunnelAsync(
            DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            // Reads the rollup, so the answer survives the purge of raw rows and
            // stays cheap over long ranges.
            var totals = await _context.AnalyticsDaily
                .AsNoTracking()
                .Where(d => d.StatDate >= from && d.StatDate <= to)
                .GroupBy(d => d.EventType)
                .Select(g => new
                {
                    EventType = g.Key,
                    Sessions = g.Sum(d => d.UniqueSessions),
                })
                .ToListAsync(ct);

            int Sessions(string type) =>
                totals.FirstOrDefault(t => t.EventType == type)?.Sessions ?? 0;

            var views = Sessions(AnalyticsEventType.PromotionView);
            var cart = Sessions(AnalyticsEventType.AddToCart);
            var checkout = Sessions(AnalyticsEventType.CheckoutStarted);
            var handoff = Sessions(AnalyticsEventType.WhatsAppClick);

            var fromDate = from.ToDateTime(TimeOnly.MinValue);
            var toDate = to.ToDateTime(TimeOnly.MaxValue);

            var orders = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate, ct);

            return new FunnelDto
            {
                PromotionViews = views,
                AddToCart = cart,
                CheckoutStarted = checkout,
                WhatsAppClicks = handoff,
                Orders = orders,
                ViewToCartRate = Rate(cart, views),
                CartToCheckoutRate = Rate(checkout, cart),
                CheckoutToHandoffRate = Rate(handoff, checkout),
            };
        }

        // ===============================
        // PER-PROMOTION PERFORMANCE
        // ===============================
        public async Task<List<PromotionPerformanceDto>> GetPromotionPerformanceAsync(
            DateOnly from, DateOnly to, int limit, CancellationToken ct = default)
        {
            var counts = await _context.AnalyticsDaily
                .AsNoTracking()
                .Where(d => d.StatDate >= from && d.StatDate <= to && d.PromotionId != null)
                .GroupBy(d => new { d.PromotionId, d.EventType })
                .Select(g => new
                {
                    PromotionId = g.Key.PromotionId!.Value,
                    g.Key.EventType,
                    Total = g.Sum(d => d.EventCount),
                })
                .ToListAsync(ct);

            var fromDate = from.ToDateTime(TimeOnly.MinValue);
            var toDate = to.ToDateTime(TimeOnly.MaxValue);

            var sales = await _context.OrderItems
                .AsNoTracking()
                .Where(i => i.PromotionId != null
                            && i.Order!.CreatedAt >= fromDate
                            && i.Order.CreatedAt <= toDate)
                .GroupBy(i => i.PromotionId!.Value)
                .Select(g => new
                {
                    PromotionId = g.Key,
                    Quantity = g.Sum(i => i.Quantity),
                    Revenue = g.Sum(i => i.LineTotal),
                })
                .ToListAsync(ct);

            var promotionIds = counts.Select(c => c.PromotionId)
                .Union(sales.Select(s => s.PromotionId))
                .Distinct()
                .ToList();

            var promotions = await _context.ItemPromotions
                .AsNoTracking()
                .Where(p => promotionIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.Status, p.SourcePromotionId })
                .ToListAsync(ct);

            var result = promotions.ConvertAll(p =>
            {
                var views = counts
                    .FirstOrDefault(c => c.PromotionId == p.Id
                                         && c.EventType == AnalyticsEventType.PromotionView)?.Total ?? 0;

                var adds = counts
                    .FirstOrDefault(c => c.PromotionId == p.Id
                                         && c.EventType == AnalyticsEventType.AddToCart)?.Total ?? 0;

                var sale = sales.FirstOrDefault(s => s.PromotionId == p.Id);

                return new PromotionPerformanceDto
                {
                    PromotionId = p.Id,
                    Name = p.Name,
                    Status = p.Status,
                    SourcePromotionId = p.SourcePromotionId,
                    Views = views,
                    AddToCart = adds,
                    OrderedQuantity = sale?.Quantity ?? 0,
                    Revenue = sale?.Revenue ?? 0m,
                    ConversionRate = Rate(adds, views),
                };
            });

            return result
                .OrderByDescending(r => r.Views)
                .ThenByDescending(r => r.Revenue)
                .Take(limit)
                .ToList();
        }

        // ===============================
        // TIME SERIES
        // ===============================
        public async Task<List<DailyPointDto>> GetTimeSeriesAsync(
            string eventType, DateOnly from, DateOnly to, CancellationToken ct = default) =>
            await _context.AnalyticsDaily
                .AsNoTracking()
                .Where(d => d.EventType == eventType && d.StatDate >= from && d.StatDate <= to)
                .GroupBy(d => d.StatDate)
                .Select(g => new DailyPointDto { Date = g.Key, Count = g.Sum(d => d.EventCount) })
                .OrderBy(p => p.Date)
                .ToListAsync(ct);

        // ===============================
        // SALES
        // ===============================
        public async Task<SalesSummaryDto> GetSalesSummaryAsync(
            DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            var fromDate = from.ToDateTime(TimeOnly.MinValue);
            var toDate = to.ToDateTime(TimeOnly.MaxValue);

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                .Select(o => new { o.Total, o.FulfillmentType })
                .ToListAsync(ct);

            if (orders.Count == 0)
                return new SalesSummaryDto();

            var revenue = orders.Sum(o => o.Total);

            return new SalesSummaryDto
            {
                Orders = orders.Count,
                Revenue = revenue,
                AverageOrderValue = decimal.Round(revenue / orders.Count, 2),
                PickupOrders = orders.Count(o => o.FulfillmentType == "pickup"),
                DeliveryOrders = orders.Count(o => o.FulfillmentType == "delivery"),
            };
        }

        // ===============================
        // ALERTS
        // ===============================
        public async Task<List<OperationalAlertDto>> GetAlertsAsync(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            var soon = now.AddDays(7);
            var alerts = new List<OperationalAlertDto>();

            var expiringSoon = await _context.ItemPromotions
                .AsNoTracking()
                .Where(p => p.Status == PromotionStatus.Active && p.DateEnd > now && p.DateEnd <= soon)
                .Select(p => new { p.Id, p.Name, p.DateEnd })
                .ToListAsync(ct);

            alerts.AddRange(expiringSoon.Select(p => new OperationalAlertDto
            {
                Kind = "expiring",
                PromotionId = p.Id,
                Message = $"\"{p.Name}\" termina em {p.DateEnd:dd/MM}.",
            }));

            var expiredButLive = await _context.ItemPromotions
                .AsNoTracking()
                .Where(p => p.Status == PromotionStatus.Active && p.DateEnd < now)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(ct);

            alerts.AddRange(expiredButLive.Select(p => new OperationalAlertDto
            {
                Kind = "expired-still-active",
                PromotionId = p.Id,
                Message = $"\"{p.Name}\" já passou da data final e continua marcada como ativa.",
            }));

            // Promotional price at or above the original: accepted on create only
            // as a guard, but an edit or a data import can still produce it.
            var invertedPrice = await _context.ItemPromotions
                .AsNoTracking()
                .Where(p => p.Status != PromotionStatus.Archived && p.Price >= p.PriceBefore)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(ct);

            alerts.AddRange(invertedPrice.Select(p => new OperationalAlertDto
            {
                Kind = "price-not-a-discount",
                PromotionId = p.Id,
                Message = $"\"{p.Name}\" tem preço promocional maior ou igual ao original.",
            }));

            var missingImages = await _context.ItemPromotions
                .AsNoTracking()
                .CountAsync(p => p.MediaAsset == null || p.MediaAsset.IsMissing, ct);

            if (missingImages > 0)
            {
                alerts.Add(new OperationalAlertDto
                {
                    Kind = "missing-image",
                    Message = $"{missingImages} promoção(ões) sem arquivo de imagem no disco.",
                });
            }

            return alerts;
        }

        private static double Rate(int numerator, int denominator) =>
            denominator == 0 ? 0 : Math.Round((double)numerator / denominator, 4);
    }
}
