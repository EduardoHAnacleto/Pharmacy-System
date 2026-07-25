using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using PharmacyWorkerAPI.DTOs;
using PharmacyWorkerAPI.DTOs.ItemPromotion;
using Xunit;

namespace PharmacyWorkerAPI.Tests.Integration;

[Collection(ApiCollection.Name)]
public class PromotionEndpointsTests
{
    private readonly ApiFixture _fixture;

    public PromotionEndpointsTests(ApiFixture fixture) => _fixture = fixture;

    private static readonly byte[] ValidPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    ];

    private static MultipartFormDataContent BuildForm(
        byte[]? image = null,
        string name = "Teste",
        decimal price = 9.90m,
        decimal priceBefore = 19.90m,
        int categoryId = 1,
        string? imageContentType = "image/png")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(name), "Name" },
            { new StringContent(price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)), "Price" },
            { new StringContent(priceBefore.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)), "PriceBefore" },
            { new StringContent("2026-01-01T00:00:00Z"), "DateStart" },
            { new StringContent("2099-12-31T00:00:00Z"), "DateEnd" },
            { new StringContent("true"), "IsActive" },
            { new StringContent(categoryId.ToString(System.Globalization.CultureInfo.InvariantCulture)), "CategoryId" },
            { new StringContent("default"), "ProductType" },
        };

        if (image != null)
        {
            var file = new ByteArrayContent(image);
            if (imageContentType != null)
                file.Headers.ContentType = new MediaTypeHeaderValue(imageContentType);

            form.Add(file, "Image", "promo.png");
        }

        return form;
    }

    // ===============================
    // AUTHORIZATION
    // ===============================

    [SkippableFact]
    public async Task Create_WithoutAToken_Returns401()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        // Before Phase 1 this endpoint was wide open to anyone reaching the API.
        var response = await client.PostAsync("/api/v1/item-promotions", BuildForm(ValidPng));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Archive_WithoutAToken_Returns401()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var response = await client.PatchAsync("/api/v1/item-promotions/1/archive", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task Delete_IsNoLongerExposed()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        // Destructive delete was removed on purpose: it took the row and the image
        // file with it, so a finished promotion could never be run again.
        var response = await client.DeleteAsync("/api/v1/item-promotions/1");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [SkippableFact]
    public async Task Reads_StayPublic()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        // It is a storefront: anonymous visitors must be able to browse.
        var response = await client.GetAsync("/api/v1/item-promotions/active?page=1&pageSize=12");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ===============================
    // VALIDATION
    // ===============================

    [SkippableFact]
    public async Task Create_RejectsAnExecutableDisguisedAsAnImage()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        // Correct Content-Type, correct extension, wrong bytes — the exact upload
        // the old content-type check accepted.
        var executable = Encoding.ASCII.GetBytes("MZ\0\0\0\0\0\0\0");

        var response = await client.PostAsync("/api/v1/item-promotions", BuildForm(executable));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Create_RejectsAPromotionalPriceAboveTheOriginal()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var response = await client.PostAsync(
            "/api/v1/item-promotions",
            BuildForm(ValidPng, price: 20m, priceBefore: 10m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Create_RejectsAnUnknownCategory()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var response = await client.PostAsync(
            "/api/v1/item-promotions", BuildForm(ValidPng, categoryId: 9999));

        // A foreign key violation would otherwise surface as a 500.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task Create_RejectsAMissingImage()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var response = await client.PostAsync("/api/v1/item-promotions", BuildForm(image: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ===============================
    // ROUND TRIP
    // ===============================

    [SkippableFact]
    public async Task Create_ThenGet_ReturnsTheSameShapeAndAnAuditedAuthor()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var created = await client.PostAsync("/api/v1/item-promotions", BuildForm(ValidPng));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var createdDto = await created.Content.ReadFromJsonAsync<ItemPromotionResponseDto>();
        Assert.NotNull(createdDto);

        // Derived from the token, not the request: the DTO no longer accepts them.
        Assert.Equal(ApiFixture.AdminUsername, createdDto!.CreatedByUserName);
        Assert.True(createdDto.CreatedByUserId > 0);

        var fetched = await client.GetFromJsonAsync<ItemPromotionResponseDto>(
            $"/api/v1/item-promotions/{createdDto.Id}");

        Assert.NotNull(fetched);
        // The single shared projection is what makes these agree; POST used to
        // return a differently shaped ImageUrl than every GET.
        Assert.Equal(createdDto.ImageUrl, fetched!.ImageUrl);
        Assert.StartsWith("/images/promotions/", fetched.ImageUrl, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task Create_InvalidatesTheCachedListing()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        // Warm the cache, then write, then read again.
        var before = await client.GetFromJsonAsync<PagedResultDto<ItemPromotionResponseDto>>(
            "/api/v1/item-promotions/active?page=1&pageSize=50");

        var created = await client.PostAsync(
            "/api/v1/item-promotions", BuildForm(ValidPng, name: "Cache probe"));
        created.EnsureSuccessStatusCode();

        var after = await client.GetFromJsonAsync<PagedResultDto<ItemPromotionResponseDto>>(
            "/api/v1/item-promotions/active?page=1&pageSize=50");

        // Version-bump invalidation must make the new row visible immediately,
        // rather than after the five-minute TTL.
        Assert.Equal(before!.TotalItems + 1, after!.TotalItems);
    }

    [SkippableFact]
    public async Task Archive_KeepsThePromotionAndItsImage()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var created = await client.PostAsync(
            "/api/v1/item-promotions", BuildForm(ValidPng, name: "To archive"));
        var dto = await created.Content.ReadFromJsonAsync<ItemPromotionResponseDto>();

        var archived = await client.PatchAsync($"/api/v1/item-promotions/{dto!.Id}/archive", null);
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        var result = await archived.Content.ReadFromJsonAsync<ItemPromotionResponseDto>();
        Assert.Equal("Archived", result!.Status);
        Assert.NotNull(result.ArchivedAt);

        // The row survives — this is the whole point of archiving — and keeps the
        // same image URL, so it can be reactivated without a re-upload.
        var fetched = await client.GetFromJsonAsync<ItemPromotionResponseDto>(
            $"/api/v1/item-promotions/{dto.Id}");

        Assert.Equal("Archived", fetched!.Status);
        Assert.Equal(dto.ImageUrl, fetched.ImageUrl);
        Assert.False(fetched.ImageMissing);
    }

    [SkippableFact]
    public async Task Archive_HidesThePromotionFromTheStorefront()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var created = await client.PostAsync(
            "/api/v1/item-promotions", BuildForm(ValidPng, name: "Hide me"));
        var dto = await created.Content.ReadFromJsonAsync<ItemPromotionResponseDto>();

        await client.PatchAsync($"/api/v1/item-promotions/{dto!.Id}/archive", null);

        var visible = await client.GetFromJsonAsync<PagedResultDto<ItemPromotionResponseDto>>(
            "/api/v1/item-promotions/active?page=1&pageSize=50");

        Assert.DoesNotContain(visible!.Items, p => p.Id == dto.Id);
    }

    [SkippableFact]
    public async Task Reactivate_ClonesWithTheSameImageAndRecordsLineage()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var created = await client.PostAsync(
            "/api/v1/item-promotions", BuildForm(ValidPng, name: "Runs again"));
        var original = await created.Content.ReadFromJsonAsync<ItemPromotionResponseDto>();

        await client.PatchAsync($"/api/v1/item-promotions/{original!.Id}/archive", null);

        var reactivated = await client.PostAsJsonAsync(
            $"/api/v1/item-promotions/{original.Id}/reactivate",
            new
            {
                dateStart = "2026-02-01T00:00:00Z",
                dateEnd = "2099-12-31T00:00:00Z",
                publish = true,
            });

        Assert.Equal(HttpStatusCode.Created, reactivated.StatusCode);

        var clone = await reactivated.Content.ReadFromJsonAsync<ItemPromotionResponseDto>();

        Assert.NotEqual(original.Id, clone!.Id);
        // Same artwork, no second upload and no second copy on disk.
        Assert.Equal(original.ImageUrl, clone.ImageUrl);
        // The chain that makes "this campaign has run before" answerable.
        Assert.Equal(original.Id, clone.SourcePromotionId);
    }

    [SkippableFact]
    public async Task Archive_AnUnknownId_Returns404()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var response = await client.PatchAsync("/api/v1/item-promotions/999999/archive", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task ArchivedPromotions_AreListedForReactivation()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = await AuthenticatedClientAsync();

        var created = await client.PostAsync(
            "/api/v1/item-promotions", BuildForm(ValidPng, name: "In the library"));
        var dto = await created.Content.ReadFromJsonAsync<ItemPromotionResponseDto>();

        await client.PatchAsync($"/api/v1/item-promotions/{dto!.Id}/archive", null);

        var library = await client.GetFromJsonAsync<PagedResultDto<ItemPromotionResponseDto>>(
            "/api/v1/item-promotions?status=Archived&page=1&pageSize=50");

        Assert.Contains(library!.Items, p => p.Id == dto.Id);
    }

    // ===============================
    // PAGING AND CATEGORIES
    // ===============================

    [SkippableFact]
    public async Task GetActivePaged_ClampsAnAbsurdPageSize()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var result = await client.GetFromJsonAsync<PagedResultDto<ItemPromotionResponseDto>>(
            "/api/v1/item-promotions/active?page=0&pageSize=5000");

        Assert.NotNull(result);
        Assert.Equal(1, result!.Page);
        Assert.Equal(12, result.PageSize);
    }

    [SkippableFact]
    public async Task GetCategories_IncludesTheIdClientsNeed()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        var categories = await client.GetFromJsonAsync<List<CategoryDto>>(
            "/api/v1/item-promotions/categories/all");

        Assert.NotNull(categories);
        Assert.NotEmpty(categories!);
        // Without the id the admin form cannot map a selection to CategoryId,
        // which is why it used to hardcode 1.
        Assert.All(categories!, c => Assert.True(c.Id > 0));
    }

    // ===============================
    // HEALTH
    // ===============================

    [SkippableFact]
    public async Task Health_ReportsLiveAndReady()
    {
        Skip.IfNot(_fixture.DockerAvailable, "Docker is not available.");
        using var client = _fixture.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        var client = _fixture.CreateClient();
        var token = await LoginHelper.GetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }
}
