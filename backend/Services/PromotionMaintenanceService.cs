using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;
using PharmacyWorkerAPI.Models;

namespace PharmacyWorkerAPI.Services
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
    }
}
