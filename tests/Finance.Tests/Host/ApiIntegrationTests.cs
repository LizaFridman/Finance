using System;
using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Tests.Host;

public class ApiIntegrationTests : IClassFixture<ApiIntegrationTests.Factory>
{
    private readonly Factory _factory;

    public ApiIntegrationTests(Factory factory) => _factory = factory;

    [Fact]
    public async Task Health_endpoint_responds_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Trend_endpoint_reflects_seeded_data_and_a_posted_bucket_move()
    {
        var db = _factory.Services.GetRequiredService<SqliteDatabase>();
        TestData.InsertTx(db, "seed-1", "2025-06-01", -250m); // no bucket yet
        var client = _factory.CreateClient();

        var trend = await client.GetFromJsonAsync<JsonElement>(
            "/api/series/trend?from=2025-06-01&to=2025-06-30");
        Assert.Equal(-250d, trend[0].GetProperty("measures").GetProperty("netBalance").GetDouble());

        var byBucket = await client.GetFromJsonAsync<JsonElement>(
            "/api/series/by-bucket?from=2025-06-01&to=2025-06-30");
        Assert.Contains(byBucket.EnumerateArray(),
            s => s.GetProperty("key").GetString() == "needs_bucket_assignment");

        var post = await client.PostAsJsonAsync(
            "/api/transactions/seed-1/bucket", new { bucketId = "shared" });
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);

        var afterExcludingNull = await client.GetFromJsonAsync<JsonElement>(
            "/api/series/trend?from=2025-06-01&to=2025-06-30&nullBucket=exclude");
        Assert.Equal(-250d, afterExcludingNull[0].GetProperty("measures").GetProperty("netBalance").GetDouble());
    }

    [Fact]
    public async Task Year_over_year_endpoint_spans_multiple_years()
    {
        var db = _factory.Services.GetRequiredService<SqliteDatabase>();
        TestData.InsertTx(db, "yoy-24", "2024-03-10", -400m);
        TestData.InsertTx(db, "yoy-25", "2025-03-10", -520m);
        var client = _factory.CreateClient();

        var yoy = await client.GetFromJsonAsync<JsonElement>("/api/series/yoy?years=2024,2025");

        var march = yoy.GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("calendarMonth").GetInt32() == 3);
        Assert.Equal(400d, march.GetProperty("byYear").GetProperty("2024").GetProperty("expense").GetDouble());
        Assert.Equal(520d, march.GetProperty("byYear").GetProperty("2025").GetProperty("expense").GetDouble());
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbPath =
            Path.Combine(Path.GetTempPath(), $"api-test-{Guid.NewGuid():N}.db");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("Finance:DatabasePath", _dbPath);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                try { File.Delete(_dbPath + suffix); } catch (IOException) { }
            }
        }
    }
}
