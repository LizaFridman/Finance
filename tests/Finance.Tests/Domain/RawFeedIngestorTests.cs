using System;
using System.IO;
using System.Linq;
using Finance.Domain.Ingestion;
using Finance.Domain.Sources;
using Finance.Infrastructure.Ingestion;
using Finance.Infrastructure.Repositories;

namespace Finance.Tests.Domain;

public class RawFeedIngestorTests : IDisposable
{
    private readonly TempDatabase _t = new();
    private readonly string _feedRoot;

    public RawFeedIngestorTests()
    {
        _feedRoot = Path.Combine(Path.GetTempPath(), $"feed-{Guid.NewGuid():N}");
        foreach (var s in new[] { "leumi", "cal", "max" })
            Directory.CreateDirectory(Path.Combine(_feedRoot, s));
    }

    public void Dispose()
    {
        _t.Dispose();
        try { Directory.Delete(_feedRoot, recursive: true); } catch (IOException) { }
    }

    private RawFeedIngestor NewIngestor() => new(
        new SourceRegistry(new SourceRepository(_t.Db)),
        new TransactionRepository(_t.Db),
        new IRawFeedParser[] { new ScraperJsonParser() });

    private void DropJson(string source, string fileName, string json)
        => File.WriteAllText(Path.Combine(_feedRoot, source, fileName), json);

    private const string TwoRows = """
    [
      { "date": "2025-02-03", "amount": -123.45, "merchantRaw": "שופרסל אונליין" },
      { "date": "2025-02-05", "amount": -50.00,  "merchantRaw": "וולט" }
    ]
    """;

    [Fact]
    public void Ingesting_a_feed_inserts_its_rows()
    {
        DropJson("leumi", "2025-02.json", TwoRows);

        var result = NewIngestor().Ingest(_feedRoot);

        Assert.Equal(2, result.Inserted);
        Assert.Equal(2, new TransactionRepository(_t.Db).Count());
    }

    [Fact]
    public void Running_the_same_import_twice_is_a_no_op()
    {
        DropJson("leumi", "2025-02.json", TwoRows);
        var ingestor = NewIngestor();

        ingestor.Ingest(_feedRoot);
        var second = ingestor.Ingest(_feedRoot);

        Assert.Equal(0, second.Inserted);
        Assert.Equal(2, second.SkippedDuplicates);
        Assert.Equal(2, new TransactionRepository(_t.Db).Count());
    }

    [Fact]
    public void An_empty_source_folder_is_tolerated_not_an_error()
    {
        DropJson("leumi", "2025-02.json", TwoRows);
        // cal/ and max/ are present but empty.

        var result = NewIngestor().Ingest(_feedRoot);

        Assert.Equal(2, result.Inserted);
        Assert.Contains(result.Messages, m => m.Contains("max") && m.Contains("no files"));
    }

    [Fact]
    public void Later_installment_rows_are_dropped_during_ingestion()
    {
        DropJson("cal", "installments.json", """
        [
          { "date": "2025-06-01", "amount": -1200.00, "merchantRaw": "מקרר",
            "installments": { "number": 1, "total": 6 } },
          { "date": "2025-07-01", "amount": -200.00, "merchantRaw": "מקרר",
            "installments": { "number": 2, "total": 6 } }
        ]
        """);

        var result = NewIngestor().Ingest(_feedRoot);

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.DroppedInstallments);
    }

    [Fact]
    public void A_native_id_in_the_feed_keys_the_row()
    {
        DropJson("leumi", "native.json", """
        [ { "date": "2025-02-03", "amount": -10.00, "merchantRaw": "x", "nativeId": "ABC123" } ]
        """);

        NewIngestor().Ingest(_feedRoot);

        Assert.True(new TransactionRepository(_t.Db).Exists("native:leumi:ABC123"));
    }
}
