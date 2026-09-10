using System;
using System.IO;
using System.Linq;
using Finance.Domain.Categorization;
using Finance.Domain.Ingestion;
using Finance.Domain.Sources;
using Finance.Infrastructure.Repositories;

namespace Finance.Tests.Domain;

public class CategorizationServiceTests : IDisposable
{
    private readonly TempDatabase _t = new();
    private readonly string _feedRoot;

    public CategorizationServiceTests()
    {
        _feedRoot = Path.Combine(Path.GetTempPath(), $"feed-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_feedRoot, "leumi"));
        File.WriteAllText(Path.Combine(_feedRoot, "leumi", "f.json"), """
        [
          { "date": "2025-02-03", "amount": -123.45, "merchantRaw": "שופרסל אונליין" },
          { "date": "2025-02-09", "amount": -50.00,  "merchantRaw": "שופרסל אונליין" }
        ]
        """);
        new RawFeedIngestor(
            new SourceRegistry(new SourceRepository(_t.Db)),
            new TransactionRepository(_t.Db),
            new IRawFeedParser[] { new ScraperJsonParser() }).Ingest(_feedRoot);
    }

    public void Dispose()
    {
        _t.Dispose();
        try { Directory.Delete(_feedRoot, true); } catch (IOException) { }
    }

    private CategorizationService NewService() => new(
        new TransactionRepository(_t.Db),
        new CategoryRepository(_t.Db),
        new MerchantDictionary(_t.Db));

    private (string firstId, string secondId) TwoIds()
    {
        var repo = new TransactionRepository(_t.Db);
        var ids = repo.WhereCategoryIsNull().Select(x => x.Id).OrderBy(x => x).ToArray();
        return (ids[0], ids[1]);
    }

    [Fact]
    public void Freshly_ingested_rows_have_no_category_and_are_needs_review()
    {
        var repo = new TransactionRepository(_t.Db);

        var pending = repo.WhereCategoryIsNull();

        Assert.Equal(2, pending.Count);
        Assert.All(pending, tx => Assert.Equal("needs_review", tx.Status));
    }

    [Fact]
    public void Confirming_a_category_creates_the_category_row_if_it_is_new()
    {
        var (id, _) = TwoIds();
        var service = NewService();

        service.ConfirmCategory(id, "groceries", "Groceries");

        Assert.Contains(new CategoryRepository(_t.Db).GetAll(), c => c.Id == "groceries");
        Assert.Equal("groceries", new TransactionRepository(_t.Db).GetById(id)!.CategoryId);
    }

    [Fact]
    public void A_confirmed_decision_auto_resolves_the_next_identical_merchant()
    {
        var (first, second) = TwoIds();
        var service = NewService();

        service.ConfirmCategory(first, "groceries", "Groceries");

        // Layer 1 now knows "שופרסל אונליין" -> groceries
        var matcher = new MerchantMatcher(new MerchantDictionary(_t.Db));
        Assert.Equal("groceries", matcher.Resolve("שופרסל אונליין"));

        // and the service can apply it to the still-unategorized twin
        Assert.True(service.TryAutoCategorize(second));
        Assert.Equal("groceries", new TransactionRepository(_t.Db).GetById(second)!.CategoryId);
    }
}
