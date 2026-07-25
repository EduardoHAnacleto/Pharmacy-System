using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PharmacyWorkerAPI.DTOs.ItemPromotion;
using Xunit;

namespace PharmacyWorkerAPI.Tests.Integration;

/// <summary>
/// Category maintenance, so a shop that is not a pharmacy can define its own.
/// </summary>
[Collection(ApiCollection.Name)]
public class CategoryEndpointsTests
{
    private readonly ApiFixture _fixture;

    public CategoryEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task Writes_RequireAToken()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var create = await client.PostAsJsonAsync("/api/v1/categories", new { name = "Pães" });
        var rename = await client.PutAsJsonAsync("/api/v1/categories/1", new { name = "Pães" });
        var delete = await client.DeleteAsync("/api/v1/categories/1");

        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rename.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [SkippableFact]
    public async Task Create_ThenRename_ThenDelete()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var name = Unique("Pães");

        var created = await client.PostAsJsonAsync("/api/v1/categories", new { name });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var category = await created.Content.ReadFromJsonAsync<CategoryDto>();
        Assert.True(category!.Id > 0);

        var renamed = Unique("Padaria");
        var rename = await client.PutAsJsonAsync(
            $"/api/v1/categories/{category.Id}", new { name = renamed });

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);

        // Read through the storefront's own endpoint: renaming must invalidate the
        // cached category list, or the admin sees a change nobody else does.
        var list = await client.GetFromJsonAsync<List<CategoryDto>>(
            "/api/v1/item-promotions/categories/all");

        Assert.Contains(list!, c => c.Id == category.Id && c.Name == renamed);

        var delete = await client.DeleteAsync($"/api/v1/categories/{category.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [SkippableFact]
    public async Task Create_RejectsADuplicateNameRegardlessOfCase()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var name = Unique("Bebidas");

        var first = await client.PostAsJsonAsync("/api/v1/categories", new { name });

        // Asserted, not ignored: without this the test passes even when the first
        // create fails for an unrelated reason, because the "duplicate" then gets
        // rejected for a different one.
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/categories", new { name = name.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [SkippableFact]
    public async Task Create_RejectsAnEmptyName()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/categories", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Create_RejectsANameLongerThanTheColumn()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        // 31 characters against a varchar(30): a 400 from validation rather than a
        // 500 from the database.
        var response = await client.PostAsJsonAsync(
            "/api/v1/categories", new { name = new string('x', 31) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Delete_RefusesACategoryInUse()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        // Creates its own category and its own promotion in it, rather than looking
        // for a promotion some other test class happens to have left behind. Tests
        // in a collection share a database but must not depend on each other's
        // order — this one skipped in CI for exactly that reason.
        var created = await client.PostAsJsonAsync(
            "/api/v1/categories", new { name = Unique("Em uso") });

        created.EnsureSuccessStatusCode();
        var category = await created.Content.ReadFromJsonAsync<CategoryDto>();

        var promotion = await client.PostAsync(
            "/api/v1/item-promotions", PromotionForm(category!.Id));

        promotion.EnsureSuccessStatusCode();

        // Deleting now would orphan that promotion. The FK is Restrict, so the
        // database would refuse anyway; this asserts the caller gets an
        // explanation naming the count instead of a 500.
        var response = await client.DeleteAsync($"/api/v1/categories/{category.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var message = await response.Content.ReadAsStringAsync();
        Assert.Contains("em uso", message, StringComparison.OrdinalIgnoreCase);
    }

    [SkippableFact]
    public async Task Rename_AnUnknownId_Returns404()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync(
            "/api/v1/categories/999999", new { name = "Qualquer" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A collision-free name that still fits the column.
    /// </summary>
    /// <remarks>
    /// Eight hex characters, not a full GUID: the name column is varchar(30), and a
    /// full GUID pushed every generated name past it. The create then failed
    /// validation, which is what it should do — but it made this suite report a
    /// defect in the endpoint rather than in the test.
    /// </remarks>
    private static string Unique(string prefix) =>
        $"{prefix} {Guid.NewGuid():N}"[..Math.Min(prefix.Length + 9, 30)];

    /// <summary>A minimal valid promotion, to hold a category in place.</summary>
    private static MultipartFormDataContent PromotionForm(int categoryId)
    {
        var png = new ByteArrayContent(
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        ]);

        png.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        var form = new MultipartFormDataContent
        {
            { new StringContent("Ocupa categoria"), "Name" },
            { new StringContent("9.90"), "Price" },
            { new StringContent("19.90"), "PriceBefore" },
            { new StringContent("2026-01-01T00:00:00Z"), "DateStart" },
            { new StringContent("2099-12-31T00:00:00Z"), "DateEnd" },
            { new StringContent("true"), "IsActive" },
            {
                new StringContent(categoryId.ToString(CultureInfo.InvariantCulture)),
                "CategoryId"
            },
            { new StringContent("default"), "ProductType" },
        };

        form.Add(png, "Image", "promo.png");

        return form;
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _fixture.CreateClient();
        var token = await LoginHelper.GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}
