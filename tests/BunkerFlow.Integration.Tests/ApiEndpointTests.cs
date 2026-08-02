using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BunkerFlow.Contracts;
using BunkerFlow.Integration.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BunkerFlow.Integration.Tests;

/// <summary>
/// Drives the real API in-process. No broker and no database are configured, so
/// the gateway runs in its loopback mode and everything ingested is queryable
/// straight away.
/// </summary>
public sealed class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory) =>
        _client = factory.CreateClient();

    [Fact]
    public async Task Should_report_liveness_without_touching_any_dependency()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Should_report_readiness_with_the_landed_count()
    {
        var response = await _client.GetAsync("/health/ready");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"ready\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_accept_a_valid_record_and_make_it_queryable()
    {
        var response = await _client.PostAsJsonAsync("/ingest", Request("api-test", "A-1"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var events = await GetEventsAsync("api-test");
        var landed = Assert.Single(events);
        Assert.Equal("TR-100045", landed.Payload.TradeReference);
        Assert.Equal(IngestionChannel.Api, landed.Channel);
    }

    [Fact]
    public async Task Should_report_a_replayed_record_as_a_duplicate()
    {
        await _client.PostAsJsonAsync("/ingest", Request("api-dupe", "D-1"));
        var second = await _client.PostAsJsonAsync("/ingest", Request("api-dupe", "D-1"));

        var body = await second.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("Duplicate", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_refuse_a_record_that_fails_data_quality()
    {
        var request = Request("api-bad", "B-1", fields =>
            fields["vesselImo"] = RecordBuilder.InvalidImo);

        var response = await _client.PostAsJsonAsync("/ingest", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("validation_failed", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_refuse_a_request_without_a_source_system()
    {
        var response = await _client.PostAsJsonAsync("/ingest", new
        {
            sourceRecordId = "X-1",
            fields = new Dictionary<string, string?> { ["tradeReference"] = "TR-1" },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Should_refuse_a_batch_larger_than_the_cap()
    {
        var oversized = Enumerable
            .Range(0, 501)
            .Select(index => Request("api-batch", $"O-{index}"))
            .ToArray();

        var response = await _client.PostAsJsonAsync("/ingest/batch", oversized);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("batch_too_large", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_summarize_a_mixed_batch_by_outcome()
    {
        var batch = new[]
        {
            Request("api-mixed", "M-1"),
            Request("api-mixed", "M-1"),
            Request("api-mixed", "M-2", fields => fields["product"] = "JETA1"),
        };

        var response = await _client.PostAsJsonAsync("/ingest/batch", batch);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"accepted\":1", body, StringComparison.Ordinal);
        Assert.Contains("\"duplicate\":1", body, StringComparison.Ordinal);
        Assert.Contains("\"rejected\":1", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_clamp_a_page_size_beyond_the_maximum()
    {
        var response = await _client.GetAsync("/events?limit=100000");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"\"limit\":{BunkerFlow.Integration.Landing.EventQuery.MaxLimit}", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_refuse_a_backwards_date_range()
    {
        var response = await _client.GetAsync("/events?from=2026-08-02T00:00:00Z&to=2026-08-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Should_expose_pipeline_counters_in_prometheus_format()
    {
        await _client.PostAsJsonAsync("/ingest", Request("api-metrics", "MT-1"));

        var response = await _client.GetAsync("/metrics");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("# TYPE bunkerflow_records_total counter", body, StringComparison.Ordinal);
        Assert.Contains("channel=\"api\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Should_serve_mock_source_data_the_batch_worker_can_pull()
    {
        var response = await _client.GetAsync("/mock-sources/erp/trades?count=5");
        var records = await response.Content.ReadFromJsonAsync<List<Dictionary<string, string?>>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(records);
        Assert.Equal(5, records.Count);
        Assert.All(records, record => Assert.True(record.ContainsKey("sourceRecordId")));
    }

    private async Task<List<IntegrationEvent>> GetEventsAsync(string sourceSystem)
    {
        var response = await _client.GetAsync($"/events?sourceSystem={sourceSystem}");
        var json = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(json);
        var items = document.RootElement.GetProperty("data").GetProperty("items");

        return JsonSerializer.Deserialize<List<IntegrationEvent>>(
            items.GetRawText(), EventSerialization.Options) ?? [];
    }

    private static object Request(
        string sourceSystem,
        string sourceRecordId,
        Action<Dictionary<string, string?>>? customize = null)
    {
        var fields = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["tradeReference"] = "TR-100045",
            ["vesselImo"] = RecordBuilder.ValidImo,
            ["port"] = "DKFRC",
            ["product"] = "VLSFO",
            ["quantityMt"] = "850.5",
            ["priceUsdPerMt"] = "612.25",
            ["counterparty"] = "Bunker Holding A/S",
            ["tradedAtUtc"] = "2026-08-01T08:30:00Z",
        };

        customize?.Invoke(fields);

        return new { sourceSystem, sourceRecordId, fields };
    }

    public void Dispose() => _client.Dispose();
}
