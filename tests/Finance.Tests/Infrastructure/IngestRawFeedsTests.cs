using System;
using System.IO;
using System.Linq;
using Finance.Application.Ingestion;
using Finance.Domain.Ingestion;
using Finance.Domain.Sources;
using Finance.Infrastructure.Ingestion;
using Finance.Infrastructure.Repositories;

namespace Finance.Tests.Infrastructure;

public class IngestRawFeedsTests : IDisposable
{
    private readonly TempDatabase _t = new();
    private readonly string _feedRoot;

    public IngestRawFeedsTests()
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

    private IngestRawFeeds NewIngest() => new(
        new SourceRegistry(new SourceRepository(_t.Db)),
        new PhysicalRawFeedFiles(_feedRoot),
        new IRawFeedParser[] { new ScraperJsonParser(), new NotImplementedRawFeedParser() },
        new TransactionRepository(_t.Db));

    private void DropJson(string source, string fileName, string json)
        => File.WriteAllText(Path.Combine(_feedRoot, source, fileName), json);

    private void DropFile(string source, string fileName, string contents)
        => File.WriteAllText(Path.Combine(_feedRoot, source, fileName), contents);

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

        var result = NewIngest().Execute();

        Assert.Equal(2, result.Inserted);
        Assert.Equal(2, new TransactionRepository(_t.Db).Count());
    }

    [Fact]
    public void Running_the_same_import_twice_is_a_no_op()
    {
        DropJson("leumi", "2025-02.json", TwoRows);
        var ingestor = NewIngest();

        ingestor.Execute();
        var second = ingestor.Execute();

        Assert.Equal(0, second.Inserted);
        Assert.Equal(2, second.SkippedDuplicates);
        Assert.Equal(2, new TransactionRepository(_t.Db).Count());
    }

    [Fact]
    public void An_empty_source_folder_is_tolerated_not_an_error()
    {
        DropJson("leumi", "2025-02.json", TwoRows);
        // cal/ and max/ are present but empty.

        var result = NewIngest().Execute();

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

        var result = NewIngest().Execute();

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.DroppedInstallments);
    }

    [Fact]
    public void A_native_id_in_the_feed_keys_the_row()
    {
        DropJson("leumi", "native.json", """
        [ { "date": "2025-02-03", "amount": -10.00, "merchantRaw": "x", "nativeId": "ABC123" } ]
        """);

        NewIngest().Execute();

        Assert.True(new TransactionRepository(_t.Db).Exists("native:leumi:ABC123"));
    }

    [Fact]
    public void An_unparseable_file_in_one_source_does_not_abort_other_sources()
    {
        DropJson("leumi", "2025-02.json", TwoRows);
        DropFile("cal", "statement.xlsm", "not a real workbook"); // .xlsm: the format with no parser yet

        var result = NewIngest().Execute();

        Assert.Equal(2, result.Inserted);
        Assert.Contains(result.Messages, m => m.Contains("cal") && m.Contains("failed to parse"));
    }

    [Fact]
    public void A_malformed_row_is_reported_but_does_not_abort_the_file()
    {
        DropJson("leumi", "mixed.json", """
        [
          { "date": "2025-02-03", "amount": -10.00, "merchantRaw": "good" },
          { "date": "not-a-date", "amount": -20.00, "merchantRaw": "bad date" },
          { "date": "2025-02-05", "amount": -30.00, "merchantRaw": "" }
        ]
        """);

        var result = NewIngest().Execute();

        Assert.Equal(1, result.Inserted); // only the first row
        Assert.Contains(result.Messages, m => m.Contains("row 2"));
        Assert.Contains(result.Messages, m => m.Contains("row 3"));
    }
}
