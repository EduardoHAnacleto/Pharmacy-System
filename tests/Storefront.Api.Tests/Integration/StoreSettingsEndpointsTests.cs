using System.Net;
using System.Net.Http.Json;
using Storefront.Api.DTOs.Store;
using Xunit;

namespace Storefront.Api.Tests.Integration;

/// <summary>
/// The endpoint that makes the project sellable to a second shop.
/// </summary>
/// <remarks>
/// Reading must stay anonymous — the storefront needs it before it can render its
/// own header — and writing must not. Everything else here is about the coherence
/// checks the annotations cannot express.
/// </remarks>
[Collection(ApiCollection.Name)]
public class StoreSettingsEndpointsTests
{
    private readonly ApiFixture _fixture;

    public StoreSettingsEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    // ===============================
    // AUTHORIZATION
    // ===============================

    [SkippableFact]
    public async Task Get_IsPublic()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var response = await client.GetAsync("/api/v1/store-settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settings = await response.Content.ReadFromJsonAsync<StoreSettingsDto>();

        // Seeded, so a fresh deployment already renders with a name and a currency.
        Assert.False(string.IsNullOrWhiteSpace(settings!.StoreName));
        Assert.Equal(3, settings.Currency.Length);
    }

    [SkippableFact]
    public async Task Put_WithoutAToken_Returns401()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/store-settings", Valid());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ===============================
    // ROUND TRIP
    // ===============================

    [SkippableFact]
    public async Task Put_ThenGet_ReturnsTheSavedConfiguration()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var update = Valid();
        update.StoreName = "Bakery on Cuba Street";
        update.Currency = "nzd";
        update.Locale = "en-NZ";
        update.CountryCode = "nz";
        update.TimeZone = "Pacific/Auckland";
        update.DeliveryFee = 12.50m;
        update.MinDeliveryTotal = 50m;
        update.DeliveryCities = ["Wellington", " Lower Hutt ", "Wellington"];
        update.CollectTaxId = false;
        update.OpeningHours = new()
        {
            ["1"] = [new() { Open = "08:00", Close = "12:00" }, new() { Open = "13:00", Close = "17:00" }],
        };

        var response = await client.PutAsJsonAsync("/api/v1/store-settings", update);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Update failed with {(int)response.StatusCode}: {body}");
        }

        // Read back anonymously: the storefront is what has to see the change.
        using var anonymous = _fixture.CreateClient();
        var saved = await anonymous.GetFromJsonAsync<StoreSettingsDto>("/api/v1/store-settings");

        Assert.Equal("Bakery on Cuba Street", saved!.StoreName);

        // Currency and country are stored upper-case regardless of how they arrive.
        Assert.Equal("NZD", saved.Currency);
        Assert.Equal("NZ", saved.CountryCode);

        Assert.Equal(12.50m, saved.DeliveryFee);
        Assert.False(saved.CollectTaxId);

        // Trimmed and de-duplicated on the way in.
        Assert.Equal(["Wellington", "Lower Hutt"], saved.DeliveryCities);

        // Two ranges for one day survive the JSON round trip, so a shop that
        // closes for lunch can say so.
        Assert.Equal(2, saved.OpeningHours["1"].Count);
        Assert.Equal("13:00", saved.OpeningHours["1"][1].Open);
    }

    [SkippableFact]
    public async Task Put_ClearsAnEmptyOptionalField()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var withTagline = Valid();
        withTagline.Tagline = "Sempre perto de você";
        await client.PutAsJsonAsync("/api/v1/store-settings", withTagline);

        var cleared = Valid();
        cleared.Tagline = null;
        await client.PutAsJsonAsync("/api/v1/store-settings", cleared);

        var saved = await client.GetFromJsonAsync<StoreSettingsDto>("/api/v1/store-settings");

        Assert.Null(saved!.Tagline);
    }

    // ===============================
    // WHATSAPP OVERRIDE FROM THE ENVIRONMENT
    // ===============================

    [SkippableFact]
    public async Task WhatsAppNumber_FromConfiguration_WinsOverTheStoredRow()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");

        // Store one number in the row, then bring up a host that configures another.
        using var seeding = await AuthenticatedClientAsync();

        var update = Valid();
        update.WhatsAppNumber = "5545999900001";
        (await seeding.PutAsJsonAsync("/api/v1/store-settings", update)).EnsureSuccessStatusCode();

        using var configured = _fixture
            .WithWebHostBuilder(b => b.UseSetting("Store:WhatsAppNumber", "6499988877"))
            .CreateClient();

        var settings = await configured.GetFromJsonAsync<StoreSettingsDto>("/api/v1/store-settings");

        // The environment is authoritative while it is set. Without this the variable
        // was write-once and inert: changing it after the first boot did nothing,
        // because the row already existed.
        Assert.Equal("6499988877", settings!.WhatsAppNumber);

        // Reported so the admin screen can render the field as managed elsewhere,
        // instead of letting an operator edit a value that cannot take effect.
        Assert.True(settings.WhatsAppNumberIsManagedByEnvironment);
    }

    [SkippableFact]
    public async Task WhatsAppNumber_WithoutConfiguration_ComesFromTheStoredRow()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var update = Valid();
        update.WhatsAppNumber = "5545999900002";

        var saved = await client.PutAsJsonAsync("/api/v1/store-settings", update);
        saved.EnsureSuccessStatusCode();

        var settings = await client.GetFromJsonAsync<StoreSettingsDto>("/api/v1/store-settings");

        Assert.Equal("5545999900002", settings!.WhatsAppNumber);
        Assert.False(settings.WhatsAppNumberIsManagedByEnvironment);
    }

    [SkippableTheory]
    [InlineData("+55 (45) 99997-5299")]
    [InlineData("not-a-number")]
    [InlineData("123")]
    public async Task WhatsAppNumber_MalformedConfiguration_IsIgnored(string configured)
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");

        using var seeding = await AuthenticatedClientAsync();

        var update = Valid();
        update.WhatsAppNumber = "5545999900003";
        (await seeding.PutAsJsonAsync("/api/v1/store-settings", update)).EnsureSuccessStatusCode();

        using var client = _fixture
            .WithWebHostBuilder(b => b.UseSetting("Store:WhatsAppNumber", configured))
            .CreateClient();

        var settings = await client.GetFromJsonAsync<StoreSettingsDto>("/api/v1/store-settings");

        // A malformed override would produce a wa.me link that silently loses every
        // order, so it is discarded in favour of the stored value.
        Assert.Equal("5545999900003", settings!.WhatsAppNumber);
        Assert.False(settings.WhatsAppNumberIsManagedByEnvironment);
    }

    [SkippableFact]
    public async Task WhatsAppNumber_OverrideIsNotBakedIntoTheCache()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");

        // Warm the cache through a host that has the override configured...
        using var overridden = _fixture
            .WithWebHostBuilder(b => b.UseSetting("Store:WhatsAppNumber", "6499911111"))
            .CreateClient();

        var forced = await overridden.GetFromJsonAsync<StoreSettingsDto>("/api/v1/store-settings");
        Assert.Equal("6499911111", forced!.WhatsAppNumber);

        // ...then read from one that does not. The override is applied after the
        // cache, so removing it takes effect on the next read rather than waiting
        // out the TTL with a stale number.
        using var plain = _fixture.CreateClient();

        var settings = await plain.GetFromJsonAsync<StoreSettingsDto>("/api/v1/store-settings");

        Assert.NotEqual("6499911111", settings!.WhatsAppNumber);
        Assert.False(settings.WhatsAppNumberIsManagedByEnvironment);
    }

    // ===============================
    // VALIDATION
    // ===============================

    [SkippableFact]
    public async Task Put_RejectsAnUnknownTimeZone()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var update = Valid();
        update.TimeZone = "Mars/Olympus_Mons";

        var response = await client.PutAsJsonAsync("/api/v1/store-settings", update);

        // Accepting it would make every opening-hours calculation throw later,
        // far away from the request that caused it.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Put_RejectsAShopThatOffersNeitherDeliveryNorPickup()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var update = Valid();
        update.DeliveryEnabled = false;
        update.PickupEnabled = false;

        var response = await client.PutAsJsonAsync("/api/v1/store-settings", update);

        // Such a shop can take no orders at all, which is never what was meant.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Put_RejectsAnInvertedOpeningRange()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var update = Valid();
        update.OpeningHours = new()
        {
            ["1"] = [new() { Open = "18:00", Close = "08:00" }],
        };

        var response = await client.PutAsJsonAsync("/api/v1/store-settings", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Put_RejectsAWeekdayOutsideOneToSeven()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var update = Valid();
        update.OpeningHours = new()
        {
            ["0"] = [new() { Open = "08:00", Close = "12:00" }],
        };

        var response = await client.PutAsJsonAsync("/api/v1/store-settings", update);

        // ISO weekdays, so Sunday is 7. Silently storing 0 would produce a day the
        // storefront never renders.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableTheory]
    [InlineData("primaryColor", "blue")]
    [InlineData("currency", "reais")]
    [InlineData("countryCode", "BRA")]
    [InlineData("whatsAppNumber", "+55 (45) 99997-5299")]
    [InlineData("email", "not-an-email")]
    public async Task Put_RejectsMalformedValues(string field, string value)
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var update = Valid();

        switch (field)
        {
            case "primaryColor": update.PrimaryColor = value; break;
            case "currency": update.Currency = value; break;
            case "countryCode": update.CountryCode = value; break;
            case "whatsAppNumber": update.WhatsAppNumber = value; break;
            case "email": update.Email = value; break;
        }

        var response = await client.PutAsJsonAsync("/api/v1/store-settings", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Put_RejectsANegativeDeliveryFee()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var update = Valid();
        update.DeliveryFee = -1m;

        var response = await client.PutAsJsonAsync("/api/v1/store-settings", update);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ===============================
    // HELPERS
    // ===============================

    /// <summary>A configuration that passes every check, for tests to spoil one field of.</summary>
    private static StoreSettingsUpdateDto Valid() => new()
    {
        StoreName = "Loja de Teste",
        PrimaryColor = "#0d6efd",
        SecondaryColor = "#198754",
        CountryCode = "BR",
        TimeZone = "America/Sao_Paulo",
        Currency = "BRL",
        Locale = "pt-BR",
        DeliveryEnabled = true,
        PickupEnabled = true,
        DeliveryFee = 8m,
        MinDeliveryTotal = 30m,
        CollectTaxId = true,
        CollectPostalCode = true,
    };

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _fixture.CreateClient();
        var token = await LoginHelper.GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}
