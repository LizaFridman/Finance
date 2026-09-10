using Finance.Domain.Model;
using Finance.Domain.Persistence;

namespace Finance.Domain.Sources;

/// <summary>
/// The source registry (spec §6). Adding a source is one config row; retiring
/// one is a status flip. A dormant source is never dropped: its historical
/// transactions still belong in the baseline import (Max is the live example).
/// </summary>
public sealed class SourceRegistry
{
    private readonly ISourceRepository _sources;

    public SourceRegistry(ISourceRepository sources)
    {
        _sources = sources;
    }

    /// <summary>Sources currently expected to produce new data.</summary>
    public IReadOnlyList<Source> ActiveSources() =>
        _sources.GetAll().Where(s => s.Status == SourceStatus.Active).ToList();

    /// <summary>
    /// Every registered source, active or dormant — a dormant source's past
    /// transactions are still part of the baseline (spec §6).
    /// </summary>
    public IReadOnlyList<Source> SourcesForBaselineImport() => _sources.GetAll();
}
