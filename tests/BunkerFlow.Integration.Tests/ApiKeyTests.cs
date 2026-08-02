using System.Net;
using System.Net.Http.Json;
using BunkerFlow.Api.Security;
using BunkerFlow.Integration.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BunkerFlow.Integration.Tests;

/// <summary>
/// The API with keys configured. The unauthenticated behaviour (no keys set)
/// is covered by <see cref="ApiEndpointTests"/>, which runs without any.
/// </summary>
public sealed class ApiKeyTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private const string ValidKey = "test-key-primary";
    private const string RotationKey = "test-key-secondary";

    private readonly HttpClient _client;

    public ApiKeyTests(WebApplicationFactory<Program> factory)
    {
        var configured = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting($"{ApiKeyOptions.SectionName}:Keys:0", ValidKey);
            builder.UseSetting($"{ApiKeyOptions.SectionName}:Keys:1", RotationKey);
        });

        _client = configured.CreateClient();
    }

    [Fact]
    public async Task Should_refuse_ingestion_without_a_key()
    {
        var response = await _client.PostAsJsonAsync("/ingest", ValidRequest("no-key", "N-1"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("api_key_missing", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_refuse_ingestion_with_the_wrong_key()
    {
        using var request = Post("/ingest", ValidRequest("bad-key", "B-1"), "not-the-key");

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("api_key_invalid", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_accept_ingestion_with_a_valid_key()
    {
        using var request = Post("/ingest", ValidRequest("good-key", "G-1"), ValidKey);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Should_accept_any_configured_key_so_rotation_does_not_break_callers()
    {
        using var request = Post("/ingest", ValidRequest("rotation", "R-1"), RotationKey);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Should_protect_the_query_endpoint_too()
    {
        var withoutKey = await _client.GetAsync("/events");

        using var withKey = new HttpRequestMessage(HttpMethod.Get, "/events");
        withKey.Headers.Add(ApiKeyOptions.HeaderName, ValidKey);
        var authorized = await _client.SendAsync(withKey);

        Assert.Equal(HttpStatusCode.Unauthorized, withoutKey.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/metrics")]
    public async Task Should_leave_operational_endpoints_open_for_probes_and_scraping(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Should_leave_the_simulated_sources_open_because_they_stand_in_for_external_systems()
    {
        var response = await _client.GetAsync("/mock-sources/erp/trades?count=3");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage Post(string path, object payload, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload),
        };

        request.Headers.Add(ApiKeyOptions.HeaderName, key);
        return request;
    }

    private static object ValidRequest(string sourceSystem, string sourceRecordId) => new
    {
        sourceSystem,
        sourceRecordId,
        fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["tradeReference"] = "TR-100045",
            ["vesselImo"] = RecordBuilder.ValidImo,
            ["port"] = "DKFRC",
            ["product"] = "VLSFO",
            ["quantityMt"] = "850.5",
            ["priceUsdPerMt"] = "612.25",
            ["counterparty"] = "Bunker Holding A/S",
            ["tradedAtUtc"] = "2026-08-01T08:30:00Z",
        },
    };

    public void Dispose() => _client.Dispose();
}
