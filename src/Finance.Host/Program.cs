using System.Text.Json.Serialization;
using Finance.Application.Categorization;
using Finance.Application.Ingestion;
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
builder.Services.AddOptions<FinanceOptions>()
    .Bind(builder.Configuration.GetSection(FinanceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var options = builder.Configuration.GetSection(FinanceOptions.SectionName).Get<FinanceOptions>()
              ?? new FinanceOptions();

// Relative paths in config are resolved against the content root.
string ResolvePath(string configured) => Path.IsPathRooted(configured)
    ? configured
    : Path.Combine(builder.Environment.ContentRootPath, configured);

var dbPath = ResolvePath(options.DatabasePath);
var rawFeedPath = ResolvePath(options.RawDataFeedPath);
var dashboardOrigins = options.DashboardOrigins;

// --- services ----------------------------------------------------------------
// Repositories open a short-lived connection per call, so singletons are fine.
builder.Services.AddSingleton(SqliteDatabase.ForFile(dbPath));

builder.Services.AddSingleton<ConfigStore>();
builder.Services.AddSingleton<IReportingConfig>(sp => sp.GetRequiredService<ConfigStore>());
builder.Services.AddSingleton<IUnitOfWork, SqliteUnitOfWork>();
builder.Services.AddSingleton<IBucketRepository, BucketRepository>();
builder.Services.AddSingleton<ICategoryRepository, CategoryRepository>();
builder.Services.AddSingleton<ISourceRepository, SourceRepository>();
builder.Services.AddSingleton<ITransactionRepository, TransactionRepository>();
builder.Services.AddSingleton<IMerchantDictionary, MerchantDictionary>();
builder.Services.AddSingleton<SourceRegistry>();
builder.Services.AddSingleton<ConfirmCategory>();
builder.Services.AddSingleton<RunCategorizationBacklog>();

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
builder.Services.AddSingleton<IRawFeedFiles>(_ => new PhysicalRawFeedFiles(rawFeedPath));
builder.Services.AddSingleton<IngestRawFeeds>();

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

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

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
