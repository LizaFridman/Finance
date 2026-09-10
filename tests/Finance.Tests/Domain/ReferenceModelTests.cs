using System.Linq;
using Finance.Domain.Model;
using Finance.Domain.Sources;
using Finance.Infrastructure.Repositories;

namespace Finance.Tests.Domain;

/// <summary>
/// Buckets, categories and sources are rows, not C# enums (spec §3): a new one
/// must appear through a data change alone, with no recompilation.
/// </summary>
public class ReferenceModelTests
{
    [Fact]
    public void A_new_bucket_row_is_visible_without_code_changes()
    {
        using var t = new TempDatabase();
        var buckets = new BucketRepository(t.Db);

        buckets.Add(new Bucket("household_pets", "Shared — Pets"));

        Assert.Contains(buckets.GetAll(), b => b.Id == "household_pets");
    }

    [Fact]
    public void A_new_category_row_is_visible_without_code_changes()
    {
        using var t = new TempDatabase();
        var categories = new CategoryRepository(t.Db);

        categories.Add(new Category("groceries", "Groceries"));

        Assert.Contains(categories.GetAll(), c => c.Id == "groceries");
    }

    [Fact]
    public void Dormant_source_Max_still_enters_the_baseline_import()
    {
        using var t = new TempDatabase();
        var registry = new SourceRegistry(new SourceRepository(t.Db));

        var baseline = registry.SourcesForBaselineImport().Select(s => s.Id).ToList();
        var active = registry.ActiveSources().Select(s => s.Id).ToList();

        Assert.Contains("max", baseline);          // dormant, but its 2025 history counts (spec §6)
        Assert.DoesNotContain("max", active);
        Assert.Contains("leumi", active);
        Assert.Contains("cal", active);
    }

    [Fact]
    public void Flipping_a_source_to_dormant_keeps_it_in_the_registry()
    {
        using var t = new TempDatabase();
        var sources = new SourceRepository(t.Db);
        var registry = new SourceRegistry(sources);

        sources.SetStatus("leumi", SourceStatus.Dormant);

        Assert.DoesNotContain("leumi", registry.ActiveSources().Select(s => s.Id));
        Assert.Contains("leumi", registry.SourcesForBaselineImport().Select(s => s.Id));
    }
}
