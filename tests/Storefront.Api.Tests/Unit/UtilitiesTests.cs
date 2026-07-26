using Storefront.Api.Utility;
using Xunit;

namespace Storefront.Api.Tests.Unit;

public class UtilitiesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetTimeZone_FallsBackToUtcWhenUnspecified(string? timeZone) =>
        Assert.Equal(TimeZoneInfo.Utc, Utilities.GetTimeZone(timeZone));

    [Fact]
    public void GetTimeZone_FallsBackToUtcForAnUnknownId()
    {
        // The value arrives from a query string, so anything can be in it. It must
        // not throw: an unhandled exception here is a 500 on the storefront.
        Assert.Equal(TimeZoneInfo.Utc, Utilities.GetTimeZone("Mars/Olympus_Mons"));
    }

    [Fact]
    public void GetTimeZone_FallsBackToUtcForAnInjectionAttempt() =>
        Assert.Equal(TimeZoneInfo.Utc, Utilities.GetTimeZone("../../etc/passwd"));

    [Fact]
    public void GetTimeZone_ResolvesAKnownIanaId()
    {
        var resolved = Utilities.GetTimeZone("America/Sao_Paulo");

        Assert.NotEqual(TimeZoneInfo.Utc, resolved);
    }

    [Fact]
    public void GetTimeZone_ResolvedIdIsStableEnoughForACacheKey()
    {
        // The paged endpoint builds its cache key from the resolved Id, so two
        // requests naming the same zone must produce the same key.
        Assert.Equal(
            Utilities.GetTimeZone("Pacific/Auckland").Id,
            Utilities.GetTimeZone("Pacific/Auckland").Id);
    }
}
