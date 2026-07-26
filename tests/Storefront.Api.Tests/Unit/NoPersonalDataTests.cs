using Microsoft.EntityFrameworkCore;
using Storefront.Api.Data;
using Storefront.Api.Models;
using Xunit;

namespace Storefront.Api.Tests.Unit;

/// <summary>
/// Enforces the privacy boundary against the actual EF model.
/// </summary>
/// <remarks>
/// A comment saying "no personal data here" decays. These tests read the mapped
/// columns, so adding a phone number to an order becomes a failing build rather
/// than something noticed after it has been collecting data for a month.
/// </remarks>
public class NoPersonalDataTests
{
    /// <summary>Substrings that must not appear in any analytics or order column.</summary>
    private static readonly string[] ForbiddenFragments =
    [
        "cpf", "phone", "telefone", "email", "mail",
        "cep", "postal", "zip", "street", "rua", "address", "endereco",
        "ip_", "ipaddress", "useragent", "user_agent", "fullname", "full_name",
        "buyer", "customer", "cliente",
    ];

    private static readonly Type[] PiiFreeEntities =
    [
        typeof(AnalyticsEvent),
        typeof(AnalyticsDaily),
        typeof(Order),
        typeof(OrderItem),
    ];

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql("server=localhost;database=x;user=x;password=x", ServerVersion.Parse("8.0"))
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void AnalyticsAndOrderTables_HaveNoPersonalDataColumns()
    {
        using var context = CreateContext();

        var offenders = new List<string>();

        foreach (var clrType in PiiFreeEntities)
        {
            var entity = context.Model.FindEntityType(clrType);
            Assert.NotNull(entity);

            foreach (var property in entity!.GetProperties())
            {
                var column = property.GetColumnName().ToLowerInvariant();

                foreach (var fragment in ForbiddenFragments)
                {
                    if (column.Contains(fragment, StringComparison.Ordinal))
                        offenders.Add($"{entity.GetTableName()}.{column}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void AnalyticsEvents_DoNotRecordAnythingThatOutlivesAVisit()
    {
        using var context = CreateContext();

        var columns = context.Model.FindEntityType(typeof(AnalyticsEvent))!
            .GetProperties()
            .Select(p => p.GetColumnName())
            .ToList();

        // The whole schema, asserted explicitly: any addition has to be a
        // deliberate edit to this list.
        Assert.Equal(
            new[] { "id", "event_type", "occurred_at", "promotion_id", "session_key" },
            columns.OrderBy(c => c == "id" ? 0 : 1).ThenBy(c => c, StringComparer.Ordinal));
    }

    [Fact]
    public void SessionKey_IsShortLivedAndNotAnIdentifier()
    {
        using var context = CreateContext();

        var sessionKey = context.Model.FindEntityType(typeof(AnalyticsEvent))!
            .GetProperty(nameof(AnalyticsEvent.SessionKey));

        // Nullable, because an event without correlation still counts, and fixed
        // width because it is a random opaque value rather than anything derived
        // from the visitor.
        Assert.True(sessionKey.IsNullable);
        Assert.Equal(32, sessionKey.GetMaxLength());
    }

    [Fact]
    public void Orders_KeepOnlyBusinessFacts()
    {
        using var context = CreateContext();

        var columns = context.Model.FindEntityType(typeof(Order))!
            .GetProperties()
            .Select(p => p.GetColumnName())
            .ToList();

        Assert.Contains("total", columns);
        Assert.Contains("item_count", columns);
        Assert.Contains("fulfillment_type", columns);

        // delivery_city is the one location field, and a city identifies nobody.
        Assert.Contains("delivery_city", columns);
        Assert.DoesNotContain("delivery_address", columns);
    }

    [Fact]
    public void OrderItems_FreezeNameAndPrice()
    {
        using var context = CreateContext();

        var columns = context.Model.FindEntityType(typeof(OrderItem))!
            .GetProperties()
            .Select(p => p.GetColumnName())
            .ToList();

        // Without the snapshots, renaming or repricing a product would silently
        // rewrite last month's revenue report.
        Assert.Contains("name_snapshot", columns);
        Assert.Contains("unit_price_snapshot", columns);
    }

    [Fact]
    public void CreateOrderDto_ExposesNoFieldForPersonalData()
    {
        // The DTO is the actual gate: a column cannot be filled by a field the API
        // does not accept.
        var properties = typeof(Storefront.Api.DTOs.Analytics.CreateOrderDto)
            .GetProperties()
            .Select(p => p.Name.ToLowerInvariant())
            .ToList();

        foreach (var fragment in ForbiddenFragments)
        {
            // deliveryCity is the deliberate exception; it is a city, not a person.
            Assert.DoesNotContain(
                properties.Where(p => p != "deliverycity"),
                p => p.Contains(fragment, StringComparison.Ordinal));
        }
    }
}
