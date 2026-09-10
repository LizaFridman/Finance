using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Finance.Application.Categorization;
using Finance.Domain;
using Finance.Domain.Categorization;
using Finance.Domain.Ingestion;
using Finance.Domain.Sources;
using Finance.Infrastructure;
using Finance.Infrastructure.Ingestion;
using Finance.Infrastructure.Repositories;

namespace Finance.Tests.Domain;

public class ConfirmCategoryTests : IDisposable
{
    private readonly TempDatabase _t = new();
    private readonly string _feedRoot;

    public ConfirmCategoryTests()
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

    private ConfirmCategory NewConfirm() => new(
        new TransactionRepository(_t.Db),
        new CategoryRepository(_t.Db),
        new MerchantDictionary(_t.Db),
        new SqliteUnitOfWork(_t.Db));

    private RunCategorizationBacklog NewBacklog() => new(
        new TransactionRepository(_t.Db),
        new MerchantDictionary(_t.Db),
        new SqliteUnitOfWork(_t.Db));

    private (string firstId, string secondId) TwoIds()
    {
        var ids = new TransactionRepository(_t.Db)
            .WhereCategoryIsNull().Select(x => x.Id).OrderBy(x => x).ToArray();
        return (ids[0], ids[1]);
    }

    [Fact]
    public void Freshly_ingested_rows_have_no_category_and_are_needs_review()
    {
        var pending = new TransactionRepository(_t.Db).WhereCategoryIsNull();

        Assert.Equal(2, pending.Count);
        Assert.All(pending, tx => Assert.Equal("needs_review", tx.Status));
    }

    [Fact]
    public void Confirming_a_category_creates_the_category_row_if_it_is_new()
    {
        var (id, _) = TwoIds();

        NewConfirm().Execute(id, "groceries", "Groceries");

        Assert.Contains(new CategoryRepository(_t.Db).GetAll(), c => c.Id == "groceries");
        Assert.Equal("groceries", new TransactionRepository(_t.Db).GetById(id)!.CategoryId);
    }

    [Fact]
    public void An_unknown_transaction_is_a_NotFound()
    {
        Assert.Throws<NotFoundException>(() => NewConfirm().Execute("no-such-id", "groceries"));
    }

    [Fact]
    public void A_confirmed_decision_auto_resolves_the_next_identical_merchant()
    {
        var (first, second) = TwoIds();

        NewConfirm().Execute(first, "groceries", "Groceries");

        // Layer 1 now knows "שופרסל אונליין" -> groceries
        Assert.Equal("groceries",
            MerchantMatcher.FromDictionary(new MerchantDictionary(_t.Db)).Resolve("שופרסל אונליין"));

        // and the backlog sweep applies it to the still-uncategorized twin
        Assert.Equal(1, NewBacklog().Execute());
        Assert.Equal("groceries", new TransactionRepository(_t.Db).GetById(second)!.CategoryId);
    }

    [Fact]
    public void A_failure_after_the_category_is_set_rolls_that_write_back_too()
    {
        var (id, _) = TwoIds();
        var confirm = new ConfirmCategory(
            new TransactionRepository(_t.Db),
            new CategoryRepository(_t.Db),
            new ThrowingMerchantDictionary(),
            new SqliteUnitOfWork(_t.Db));

        Assert.Throws<InvalidOperationException>(() => confirm.Execute(id, "groceries"));

        Assert.Null(new TransactionRepository(_t.Db).GetById(id)!.CategoryId); // SetCategory rolled back
        Assert.Empty(new CategoryRepository(_t.Db).GetAll());                  // Add rolled back
    }

    private sealed class ThrowingMerchantDictionary : IMerchantDictionary
    {
        public string? Resolve(string merchantRaw) => null;
        public IReadOnlyList<KeyValuePair<string, string>> Entries() => [];
        public void Upsert(string merchantRaw, string categoryId) =>
            throw new InvalidOperationException("boom");
    }
}
