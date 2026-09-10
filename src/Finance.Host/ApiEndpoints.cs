using Finance.Application.Categorization;
using Finance.Application.Reporting;
using Finance.Domain.Ingestion;
using Finance.Domain.Persistence;
using Finance.Domain.Reporting;

namespace Finance.Host;

/// <summary>
/// Read endpoints are all live queries; there is no static export step, because a
/// static file goes stale the moment a second device is viewing it (spec §0/§14 P4).
/// The two POST endpoints are the Phase 2–3 review-loop tooling.
/// </summary>
public static class ApiEndpoints
{
    public static void MapFinanceApi(this WebApplication app)
    {
        app.MapGet("/api/health", (SqliteDatabaseInfo info) => Results.Ok(new
        {
            status = "ok",
            database = info.Path,
            utc = DateTime.UtcNow,
        }));

        // --- reference data ------------------------------------------------
        app.MapGet("/api/reference/buckets", (IBucketRepository r) => Results.Ok(r.GetAll()));
        app.MapGet("/api/reference/categories", (ICategoryRepository r) => Results.Ok(r.GetAll()));
        app.MapGet("/api/reference/sources", (ISourceRepository r) => Results.Ok(r.GetAll()));

        // --- transactions ------------------------------------------------------
        app.MapGet("/api/transactions", (
            ITransactionRepository repo,
            string? status, string? bucket, DateOnly? from, DateOnly? to,
            bool uncategorized = false, bool unbucketed = false, int limit = 500) =>
            Results.Ok(repo.Query(status, bucket, from, to, uncategorized, unbucketed, limit)));

        // --- time series (the primary surface) -------------------------------
        app.MapGet("/api/series", (HttpRequest req, ITimeSeriesReporting q) =>
        {
            var (from, to) = req.Range();
            return Results.Ok(q.Series(req.Grain(), from, to, req.Filter()));
        });

        app.MapGet("/api/series/trend", (HttpRequest req, ITimeSeriesReporting q) =>
        {
            var (from, to) = req.Range();
            return Results.Ok(q.MonthlyTrend(from, to, req.Filter()));
        });

        app.MapGet("/api/series/cumulative", (HttpRequest req, ITimeSeriesReporting q) =>
        {
            var (from, to) = req.Range();
            return Results.Ok(q.CumulativeWithinYear(from, to, req.Filter()));
        });

        app.MapGet("/api/series/rolling12", (HttpRequest req, ITimeSeriesReporting q) =>
        {
            var (from, to) = req.Range();
            return Results.Ok(q.Rolling12Month(from, to, req.Filter()));
        });

        app.MapGet("/api/series/yoy", (HttpRequest req, ITimeSeriesReporting q) =>
        {
            var years = (req.Query["years"].ToString())
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse).ToArray();
            if (years.Length == 0)
                return Results.BadRequest(new { error = "pass ?years=2025,2026" });
            return Results.Ok(q.YearOverYear(years, req.Filter()));
        });

        app.MapGet("/api/series/by-bucket", (HttpRequest req, ITimeSeriesReporting q) =>
        {
            var (from, to) = req.Range();
            return Results.Ok(q.SeriesByBucket(req.Grain(), from, to, req.Filter()));
        });

        app.MapGet("/api/series/by-category", (HttpRequest req, ITimeSeriesReporting q) =>
        {
            var (from, to) = req.Range();
            return Results.Ok(q.SeriesByCategory(req.Grain(), from, to, req.Filter()));
        });

        // --- ingestion trigger (Phase 1) -----------------------------------
        // Reads every Raw Data Feed/<source>/ folder and inserts new transactions.
        // Idempotent: re-running only adds rows whose key isn't already present.
        app.MapPost("/api/ingest", (RawFeedIngestor ingestor, RawFeedPath feed) =>
            Results.Ok(ingestor.Ingest(feed.Path)));

        // --- review-loop writes (Phases 2–3) --------------------------------
        app.MapPost("/api/transactions/{id}/category", (
            string id, ConfirmCategoryRequest body,
            ConfirmCategory confirm, RunCategorizationBacklog backlog) =>
        {
            confirm.Execute(id, body.CategoryId, body.CategoryLabel);
            var autoResolved = backlog.Execute();
            return Results.Ok(new { confirmed = id, autoResolved });
        });

        app.MapPost("/api/transactions/{id}/bucket", (
            string id, AssignBucketRequest body, ITransactionRepository repo) =>
        {
            repo.SetBucket(id, string.IsNullOrWhiteSpace(body.BucketId) ? null : body.BucketId);
            return Results.Ok(new { assigned = id, bucket = body.BucketId });
        });
    }

    private static (DateOnly From, DateOnly To) Range(this HttpRequest req)
    {
        var to = req.Query.TryGetValue("to", out var toRaw) && DateOnly.TryParse(toRaw, out var t)
            ? t : DateOnly.FromDateTime(DateTime.UtcNow);
        var from = req.Query.TryGetValue("from", out var fromRaw) && DateOnly.TryParse(fromRaw, out var f)
            ? f : to.AddYears(-1);
        return (from, to);
    }

    private static Grain Grain(this HttpRequest req) =>
        Enum.TryParse<Grain>(req.Query["grain"], ignoreCase: true, out var g) ? g : Finance.Domain.Reporting.Grain.Month;

    private static SeriesFilter Filter(this HttpRequest req) => new()
    {
        SourceId = req.Query["sourceId"].FirstOrDefault(),
        CategoryId = req.Query["categoryId"].FirstOrDefault(),
        BucketId = req.Query["bucketId"].FirstOrDefault(),
        HidePersonalAkumu = req.Query["hidePersonal"] == "true",
        NullBuckets = req.Query["nullBucket"] == "exclude"
            ? NullBucketHandling.Exclude
            : NullBucketHandling.Include,
    };
}

public sealed record ConfirmCategoryRequest(string CategoryId, string? CategoryLabel = null);
public sealed record AssignBucketRequest(string? BucketId);

/// <summary>Tiny wrapper so <c>/api/health</c> can report the resolved database path.</summary>
public sealed record SqliteDatabaseInfo(string Path);

/// <summary>The resolved <c>Raw Data Feed</c> root the ingestion endpoint reads from.</summary>
public sealed record RawFeedPath(string Path);
