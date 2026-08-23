using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Storefront.Api.DTOs;
using Storefront.Api.DTOs.ItemPromotion;
using Xunit;

namespace Storefront.Api.Tests.Integration;

/// <summary>
/// Guards the one place this API's number parsing depended on the machine it ran
/// on.
/// </summary>
/// <remarks>
/// ASP.NET binds query-string values with the invariant culture but form values
/// with <see cref="CultureInfo.CurrentCulture"/>. Prices reach this application
/// as multipart form fields, so on a host whose culture writes decimals with a
/// comma the "3.00" a browser submits used to bind as 300 — silently, and on a
/// price.
/// <para>
/// The thread culture is pinned here rather than left to the machine, so these
/// tests mean the same thing on a build agent in any locale instead of passing
/// everywhere except Brazil.
/// </para>
/// <para>
/// <see cref="TestServer.PreserveExecutionContext"/> has to be turned on for
/// that pinning to reach the server at all. It is false by default, so the
/// pipeline runs on a context that never sees the calling thread's culture and
/// quietly falls back to the process default — which means these tests passed
/// against the very defect they exist to catch, on the first attempt, before
/// this line was added. With it on, an explicitly set thread culture beats
/// DefaultThreadCurrentCulture, so they can only pass because
/// UseRequestLocalization pins the request as well.
/// </para>
/// <para>
/// Note what is deliberately NOT done: the fixture's own culture is left alone.
/// Pinning the culture around the whole suite would make the symptom disappear
/// while leaving the application exactly as fragile, and these assertions are
/// the only thing that would catch it coming back.
/// </para>
/// </remarks>
[Collection(ApiCollection.Name)]
public class FormCultureTests
{
    private readonly ApiFixture _fixture;

    public FormCultureTests(ApiFixture fixture) => _fixture = fixture;

    /// <summary>A culture that writes 3.00 as "3,00".</summary>
    private static readonly CultureInfo CommaDecimal = new("pt-BR");

    private static readonly byte[] ValidPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    ];

    /// <summary>
    /// Runs <paramref name="body"/> with the calling context pinned to a
    /// comma-decimal culture and the server set to carry that context into the
    /// pipeline. Both are restored afterwards: the collection runs these tests
    /// serially, but leaving either one flipped would change what every later
    /// test is measuring.
    /// </summary>
    private async Task UnderCommaDecimalCultureAsync(Func<HttpClient, Task> body)
    {
        var server = _fixture.Server;
        var previousPreserve = server.PreserveExecutionContext;
        var previousCulture = CultureInfo.CurrentCulture;

        server.PreserveExecutionContext = true;
        CultureInfo.CurrentCulture = CommaDecimal;

        try
        {
            using var client = _fixture.CreateClient();
            var token = await LoginHelper.GetAccessTokenAsync(client);
            client.DefaultRequestHeaders.Authorization = new("Bearer", token);

            await body(client);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            server.PreserveExecutionContext = previousPreserve;
        }
    }

    /// <summary>
    /// The create form exactly as a browser sends it: a dot decimal separator,
    /// which is what an &lt;input type="number"&gt; submits whatever the user's
    /// locale.
    /// </summary>
    private static MultipartFormDataContent BuildForm(string name, string price, string priceBefore)
    {
        var image = new ByteArrayContent(ValidPng);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        return new MultipartFormDataContent
        {
            { new StringContent(name), "Name" },
            { new StringContent(price), "Price" },
            { new StringContent(priceBefore), "PriceBefore" },
            { new StringContent("2026-01-01T00:00:00Z"), "DateStart" },
            { new StringContent("2099-12-31T00:00:00Z"), "DateEnd" },
            { new StringContent("true"), "Publish" },
            { new StringContent("1"), "CategoryId" },
            { new StringContent("default"), "ProductType" },
            { image, "Image", "promo.png" },
        };
    }

    [SkippableFact]
    public async Task Create_UnderACommaDecimalCulture_KeepsTheSubmittedPrice()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");

        await UnderCommaDecimalCultureAsync(async client =>
        {
            var marker = $"Cultura{Guid.NewGuid():N}";

            var response = await client.PostAsync(
                "/api/v1/item-promotions",
                BuildForm(marker, price: "3.00", priceBefore: "9.00"));

            response.EnsureSuccessStatusCode();

            var created = await response.Content.ReadFromJsonAsync<ItemPromotionResponseDto>();

            // The failure this guards against is 300.00 and 900.00: the dot read
            // as a group separator rather than a decimal point.
            Assert.Equal(3.00m, created!.Price);
            Assert.Equal(9.00m, created.PriceBefore);

            // And it survives the round trip through the database, so the defect
            // cannot hide in a response assembled from the request.
            var fetched = await client.GetFromJsonAsync<ItemPromotionResponseDto>(
                $"/api/v1/item-promotions/{created.Id}");

            Assert.Equal(3.00m, fetched!.Price);
        });
    }

    [SkippableFact]
    public async Task PriceFilters_UnderACommaDecimalCulture_MatchOnTheSubmittedValue()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");

        await UnderCommaDecimalCultureAsync(async client =>
        {
            var marker = $"Filtro{Guid.NewGuid():N}";

            var response = await client.PostAsync(
                "/api/v1/item-promotions",
                BuildForm(marker, price: "3.00", priceBefore: "9.00"));

            response.EnsureSuccessStatusCode();

            // Query-string values already bound invariantly, so this half was
            // never broken. It is here because the two halves have to agree:
            // storing 300 while filtering on 3.00 is how the defect presented
            // originally — as a search that returned nothing.
            var inRange = await client.GetFromJsonAsync<PagedResultDto<ItemPromotionResponseDto>>(
                $"/api/v1/item-promotions/active?filter.search={marker}"
                + "&filter.minPrice=2.50&filter.maxPrice=3.50");

            Assert.Single(inRange!.Items);
            Assert.Equal(3.00m, inRange.Items[0].Price);
        });
    }
}
