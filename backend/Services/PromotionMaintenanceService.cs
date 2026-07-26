using Microsoft.EntityFrameworkCore;
using Storefront.Api.Data;
using Storefront.Api.Models;

namespace Storefront.Api.Services
{
    /// <summary>
    /// Periodically writes down status transitions that time causes.
    /// </summary>
    /// <remarks>
    /// Expiry used to be only a query filter, so the fact of a promotion expiring
    /// was never recorded anywhere — making "how long did it run" and "was it
    /// pulled early" unanswerable. This job records those transitions.
    /// <para>
    /// It deliberately does not gate visibility: the storefront evaluates the date
    /// window per request, so a promotion appears the moment its window opens
    /// rather than whenever this next happens to run.
    /// </para>
    /// </remarks>
    public class PromotionMaintenanceService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

        private readonly IServiceProvider _services;
        private readonly ILogger<PromotionMaintenanceService> _logger;

        public PromotionMaintenanceService(
            IServiceProvider services, ILogger<PromotionMaintenanceService> logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(Interval);

            do
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // A failed sweep must never take the API down with it.
                    _logger.LogError(ex, "Promotion maintenance sweep failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        internal async Task RunOnceAsync(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            await RecordStatusTransitionsAsync(context, ct);
            await RollUpAnalyticsAsync(context, configuration, ct);
        }

        private async Task RecordStatusTransitionsAsync(AppDbContext context, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            // Scheduled -> Active, and Active/Scheduled -> Expired.
            var candidates = await context.ItemPromotions
                .Where(p => p.Status == PromotionStatus.Scheduled || p.Status == PromotionStatus.Active)
                .ToListAsync(ct);

            var transitions = 0;

            foreach (var promotion in candidates)
            {
                var target = promotion switch
                {
                    { DateEnd: var end } when now > end => PromotionStatus.Expired,
                    { DateStart: var start } when now >= start => PromotionStatus.Active,
                    _ => PromotionStatus.Scheduled,
                };

                if (target == promotion.Status)
                    continue;

                context.PromotionStatusHistory.Add(new PromotionStatusHistory
                {
                    PromotionId = promotion.Id,
                    FromStatus = promotion.Status,
                    ToStatus = target,
                    // Null actor: the clock did this, not a person.
                    ChangedByUserId = null,
                    Reason = "window",
                    ChangedAt = now,
                });

                promotion.Status = target;
                transitions++;
            }

            if (transitions > 0)
            {
                await context.SaveChangesAsync(ct);
                _logger.LogInformation("Recorded {Count} promotion status transitions.", transitions);
            }
        }

        /// <summary>
        /// Aggregates raw events into daily counts, then purges the raw rows past
        /// the retention window.
        /// </summary>
        /// <remarks>
        /// This is both the cost control and the privacy control: the aggregate
        /// keeps reporting cheap and available indefinitely, while the per-visit
        /// session keys — the only thing linking two events together — stop existing
        /// once their day has been rolled up.
        /// </remarks>
        private async Task RollUpAnalyticsAsync(
            AppDbContext context, IConfiguration configuration, CancellationToken ct)
        {
            var retentionDays = configuration.GetValue<int?>("Analytics:RawRetentionDays") ?? 90;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Only complete days are rolled up; today is still accumulating.
            var pendingDates = await context.AnalyticsEvents
                .Select(e => DateOnly.FromDateTime(e.OccurredAt))
                .Distinct()
                .Where(d => d < today)
                .ToListAsync(ct);

            foreach (var date in pendingDates)
            {
                var dayStart = date.ToDateTime(TimeOnly.MinValue);
                var dayEnd = date.ToDateTime(TimeOnly.MaxValue);

                var aggregates = await context.AnalyticsEvents
                    .Where(e => e.OccurredAt >= dayStart && e.OccurredAt <= dayEnd)
                    .GroupBy(e => new { e.EventType, e.PromotionId })
                    .Select(g => new
                    {
                        g.Key.EventType,
                        g.Key.PromotionId,
                        EventCount = g.Count(),
                        UniqueSessions = g.Select(e => e.SessionKey).Distinct().Count(),
                    })
                    .ToListAsync(ct);

                foreach (var aggregate in aggregates)
                {
                    // Upsert, so re-running the sweep is idempotent.
                    var existing = await context.AnalyticsDaily
                        .FirstOrDefaultAsync(
                            d => d.StatDate == date
                                 && d.EventType == aggregate.EventType
                                 && d.PromotionId == aggregate.PromotionId,
                            ct);

                    if (existing == null)
                    {
                        context.AnalyticsDaily.Add(new AnalyticsDaily
                        {
                            StatDate = date,
                            EventType = aggregate.EventType,
                            PromotionId = aggregate.PromotionId,
                            EventCount = aggregate.EventCount,
                            UniqueSessions = aggregate.UniqueSessions,
                        });
                    }
                    else
                    {
                        existing.EventCount = aggregate.EventCount;
                        existing.UniqueSessions = aggregate.UniqueSessions;
                    }
                }

                await context.SaveChangesAsync(ct);
            }

            // Purge only what has been aggregated.
            var cutoff = DateTime.UtcNow.AddDays(-retentionDays);

            var purged = await context.AnalyticsEvents
                .Where(e => e.OccurredAt < cutoff)
                .ExecuteDeleteAsync(ct);

            if (purged > 0)
                _logger.LogInformation("Purged {Count} raw analytics events past retention.", purged);
        }
    }
}
