using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PharmacyWorkerAPI.Data;

namespace PharmacyWorkerAPI.Services
{
    public interface IExportService
    {
        /// <summary>Datasets available for export.</summary>
        IReadOnlyList<string> Datasets { get; }

        bool IsKnown(string dataset);

        /// <summary>
        /// Streams a dataset as CSV rows into <paramref name="output"/>.
        /// </summary>
        Task WriteCsvAsync(
            string dataset, DateOnly from, DateOnly to, Stream output, CancellationToken ct = default);
    }

    /// <summary>
    /// Streaming CSV export.
    /// </summary>
    /// <remarks>
    /// Written straight to the response body rather than buffered: a year of order
    /// lines should not have to fit in memory to be downloaded.
    /// <para>
    /// Encoded UTF-8 with a byte order mark, because Excel in a pt-BR locale
    /// otherwise renders accented product names as mojibake — the difference
    /// between a usable export and one the recipient assumes is broken.
    /// </para>
    /// </remarks>
    public class ExportService : IExportService
    {
        private static readonly string[] KnownDatasets =
        [
            "promotions",
            "promotion-performance",
            "funnel-daily",
            "orders",
            "order-items",
            "audit-log",
        ];

        public IReadOnlyList<string> Datasets => KnownDatasets;

        private readonly AppDbContext _context;
        private readonly IAnalyticsService _analytics;

        public ExportService(AppDbContext context, IAnalyticsService analytics)
        {
            _context = context;
            _analytics = analytics;
        }

        public bool IsKnown(string dataset) => KnownDatasets.Contains(dataset);

        public async Task WriteCsvAsync(
            string dataset, DateOnly from, DateOnly to, Stream output, CancellationToken ct = default)
        {
            await using var writer = new StreamWriter(
                output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);

            var fromDate = from.ToDateTime(TimeOnly.MinValue);
            var toDate = to.ToDateTime(TimeOnly.MaxValue);

            switch (dataset)
            {
                case "promotions":
                    await WritePromotionsAsync(writer, ct);
                    break;

                case "promotion-performance":
                    await WritePerformanceAsync(writer, from, to, ct);
                    break;

                case "funnel-daily":
                    await WriteFunnelDailyAsync(writer, from, to, ct);
                    break;

                case "orders":
                    await WriteOrdersAsync(writer, fromDate, toDate, ct);
                    break;

                case "order-items":
                    await WriteOrderItemsAsync(writer, fromDate, toDate, ct);
                    break;

                case "audit-log":
                    await WriteAuditLogAsync(writer, fromDate, toDate, ct);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(dataset), dataset, "Unknown dataset.");
            }

            await writer.FlushAsync(ct);
        }

        // ===============================
        // DATASETS
        // ===============================

        private async Task WritePromotionsAsync(StreamWriter writer, CancellationToken ct)
        {
            await WriteRowAsync(writer, ct,
                "id", "name", "price", "price_before", "status", "date_start", "date_end",
                "source_promotion_id", "created_by", "created_at", "archived_at", "image_missing");

            var rows = _context.ItemPromotions
                .AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.PriceBefore,
                    p.Status,
                    p.DateStart,
                    p.DateEnd,
                    p.SourcePromotionId,
                    p.CreatedByUserName,
                    p.CreatedAt,
                    p.ArchivedAt,
                    ImageMissing = p.MediaAsset == null || p.MediaAsset.IsMissing,
                })
                .AsAsyncEnumerable();

            await foreach (var row in rows.WithCancellation(ct))
            {
                await WriteRowAsync(writer, ct,
                    Num(row.Id), row.Name, Num(row.Price), Num(row.PriceBefore), row.Status,
                    Date(row.DateStart), Date(row.DateEnd), Num(row.SourcePromotionId),
                    row.CreatedByUserName, Date(row.CreatedAt), Date(row.ArchivedAt),
                    row.ImageMissing ? "true" : "false");
            }
        }

        private async Task WritePerformanceAsync(
            StreamWriter writer, DateOnly from, DateOnly to, CancellationToken ct)
        {
            await WriteRowAsync(writer, ct,
                "promotion_id", "name", "status", "source_promotion_id",
                "views", "add_to_cart", "ordered_quantity", "revenue", "conversion_rate");

            var rows = await _analytics.GetPromotionPerformanceAsync(from, to, int.MaxValue, ct);

            foreach (var row in rows)
            {
                await WriteRowAsync(writer, ct,
                    Num(row.PromotionId), row.Name, row.Status, Num(row.SourcePromotionId),
                    Num(row.Views), Num(row.AddToCart), Num(row.OrderedQuantity),
                    Num(row.Revenue), Num(row.ConversionRate));
            }
        }

        private async Task WriteFunnelDailyAsync(
            StreamWriter writer, DateOnly from, DateOnly to, CancellationToken ct)
        {
            await WriteRowAsync(writer, ct,
                "stat_date", "event_type", "promotion_id", "event_count", "unique_sessions");

            var rows = _context.AnalyticsDaily
                .AsNoTracking()
                .Where(d => d.StatDate >= from && d.StatDate <= to)
                .OrderBy(d => d.StatDate)
                .ThenBy(d => d.EventType)
                .AsAsyncEnumerable();

            await foreach (var row in rows.WithCancellation(ct))
            {
                await WriteRowAsync(writer, ct,
                    row.StatDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    row.EventType, Num(row.PromotionId), Num(row.EventCount), Num(row.UniqueSessions));
            }
        }

        private async Task WriteOrdersAsync(
            StreamWriter writer, DateTime from, DateTime to, CancellationToken ct)
        {
            // No customer columns exist to export.
            await WriteRowAsync(writer, ct,
                "order_number", "created_at", "fulfillment_type", "payment_method",
                "delivery_city", "items_subtotal", "delivery_fee", "total", "item_count");

            var rows = _context.Orders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= from && o.CreatedAt <= to)
                .OrderBy(o => o.CreatedAt)
                .AsAsyncEnumerable();

            await foreach (var row in rows.WithCancellation(ct))
            {
                await WriteRowAsync(writer, ct,
                    row.OrderNumber, Date(row.CreatedAt), row.FulfillmentType, row.PaymentMethod,
                    row.DeliveryCity, Num(row.ItemsSubtotal), Num(row.DeliveryFee),
                    Num(row.Total), Num(row.ItemCount));
            }
        }

        private async Task WriteOrderItemsAsync(
            StreamWriter writer, DateTime from, DateTime to, CancellationToken ct)
        {
            await WriteRowAsync(writer, ct,
                "order_number", "created_at", "promotion_id", "name", "unit_price",
                "quantity", "line_total");

            var rows = _context.OrderItems
                .AsNoTracking()
                .Where(i => i.Order!.CreatedAt >= from && i.Order.CreatedAt <= to)
                .OrderBy(i => i.OrderId)
                .Select(i => new
                {
                    i.Order!.OrderNumber,
                    i.Order.CreatedAt,
                    i.PromotionId,
                    // The snapshot, not the promotion's current name.
                    i.NameSnapshot,
                    i.UnitPriceSnapshot,
                    i.Quantity,
                    i.LineTotal,
                })
                .AsAsyncEnumerable();

            await foreach (var row in rows.WithCancellation(ct))
            {
                await WriteRowAsync(writer, ct,
                    row.OrderNumber, Date(row.CreatedAt), Num(row.PromotionId), row.NameSnapshot,
                    Num(row.UnitPriceSnapshot), Num(row.Quantity), Num(row.LineTotal));
            }
        }

        private async Task WriteAuditLogAsync(
            StreamWriter writer, DateTime from, DateTime to, CancellationToken ct)
        {
            await WriteRowAsync(writer, ct,
                "occurred_at", "user_id", "user_name", "action", "entity_type", "entity_id", "changes");

            var rows = _context.AuditLog
                .AsNoTracking()
                .Where(a => a.OccurredAt >= from && a.OccurredAt <= to)
                .OrderBy(a => a.OccurredAt)
                .AsAsyncEnumerable();

            await foreach (var row in rows.WithCancellation(ct))
            {
                await WriteRowAsync(writer, ct,
                    Date(row.OccurredAt), Num(row.UserId), row.UserName, row.Action,
                    row.EntityType, Num(row.EntityId), row.Changes);
            }
        }

        // ===============================
        // CSV PRIMITIVES
        // ===============================

        private static async Task WriteRowAsync(
            StreamWriter writer, CancellationToken ct, params string?[] fields)
        {
            ct.ThrowIfCancellationRequested();

            await writer.WriteLineAsync(string.Join(',', fields.Select(Escape)));
        }

        private static string Escape(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            // A leading =, +, - or @ makes Excel treat the cell as a formula, which
            // is both a correctness and a security problem for exported text.
            var needsGuard = field[0] is '=' or '+' or '-' or '@';
            var value = needsGuard ? "'" + field : field;

            if (value.Contains(',', StringComparison.Ordinal)
                || value.Contains('"', StringComparison.Ordinal)
                || value.Contains('\n', StringComparison.Ordinal)
                || value.Contains('\r', StringComparison.Ordinal))
            {
                return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
            }

            return value;
        }

        // Invariant culture throughout: a decimal comma would break the column
        // separator, and dates must be machine-readable.
        private static string Num<T>(T value) where T : struct, IFormattable =>
            value.ToString(null, CultureInfo.InvariantCulture);

        private static string Num<T>(T? value) where T : struct, IFormattable =>
            value?.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;

        private static string Date(DateTime value) =>
            value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        private static string Date(DateTime? value) =>
            value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
    }
}
