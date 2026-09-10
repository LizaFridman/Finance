using System.Text.Json.Serialization;
using Finance.Application.Reporting;
using Finance.Domain.Categorization;
using Finance.Domain.Ingestion;
using Finance.Domain.Persistence;
using Finance.Domain.Reporting;
using Finance.Domain.Sources;
using Finance.Infrastructure;
using Finance.Infrastructure.Ingestion;
using Finance.Infrastructure.Reporting;
using Finance.Infrastructure.Repositories;
using Finance.Host;

var builder = WebApplication.CreateBuilder(args);

// --- configuration -------------------------------------------------------------
// The database path is resolved relative to the content root unless absolute.
var dbPath = builder.Configuration["Finance:DatabasePath"] ?? "finance.db";
if (!Path.IsPathRooted(dbPath))
    dbPath = Path.Combine(builder.Environment.ContentRootPath, dbPath);

var dashboardOrigins = builder.Configuration
    .GetSection("Finance:DashboardOrigins").Get<string[]>() ?? [];

// --- services ----------------------------------------------------------------
// Repositories open a short-lived connection per call, so singletons are fine.
builder.Services.AddSingleton(new SqliteDatabase(
    new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    {
        DataSource = dbPath,
        ForeignKeys = true,
    }.ConnectionString));
builder.Services.AddSingleton(new SqliteDatabaseInfo(dbPath));

builder.Services.AddSingleton<ConfigStore>();
builder.Services.AddSingleton<IReportingConfig>(sp => sp.GetRequiredService<ConfigStore>());
builder.Services.AddSingleton<IBucketRepository, BucketRepository>();
builder.Services.AddSingleton<ICategoryRepository, CategoryRepository>();
builder.Services.AddSingleton<ISourceRepository, SourceRepository>();
builder.Services.AddSingleton<ITransactionRepository, TransactionRepository>();
builder.Services.AddSingleton<IMerchantDictionary, MerchantDictionary>();
builder.Services.AddSingleton<SourceRegistry>();
builder.Services.AddSingleton<CategorizationService>();

// Time-series reporting: SQL row-reader (infra) + pure calculator (domain),
// composed in the application ring.
builder.Services.AddSingleton<ITransactionRowReader, TransactionRowReader>();
builder.Services.AddSingleton<TimeSeriesCalculator>();
builder.Services.AddSingleton<ITimeSeriesReporting, TimeSeriesReporting>();

// Ingestion pipeline (spec §14 P0.5 — "underneath" the host). Scraper-JSON is the
// only feed format implemented; the NotImplemented parser claims binary formats
// so an accidental drop fails loudly.
builder.Services.AddSingleton<IRawFeedParser, ScraperJsonParser>();
builder.Services.AddSingleton<IRawFeedParser, NotImplementedRawFeedParser>();
builder.Services.AddSingleton<InstallmentPolicy>();
builder.Services.AddSingleton<RawFeedIngestor>();

var rawFeedPath = builder.Configuration["Finance:RawDataFeedPath"] ?? "Raw Data Feed";
if (!Path.IsPathRooted(rawFeedPath))
    rawFeedPath = Path.Combine(builder.Environment.ContentRootPath, rawFeedPath);
builder.Services.AddSingleton(new RawFeedPath(rawFeedPath));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddCors(options => options.AddPolicy("dashboard", policy =>
{
    // No auth layer at all — Tailscale membership is the access boundary (spec §0).
    // CORS just lets the dashboard's browser origin call the API.
    if (dashboardOrigins.Length > 0)
        policy.WithOrigins(dashboardOrigins).AllowAnyHeader().AllowAnyMethod();
    else
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
}));

builder.Services.AddOpenApi();

var app = builder.Build();

// Create/upgrade the schema on start-up; idempotent (spec §4.3).
app.Services.GetRequiredService<SqliteDatabase>().Bootstrap();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// No UseHttpsRedirection: tailnet traffic is plain HTTP over the private network
// (spec §4.3); an HTTPS redirect would break access from phones on the tailnet.
app.UseCors("dashboard");

app.MapFinanceApi();

app.Run();

/// <summary>Exposed so integration tests can host the app with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
