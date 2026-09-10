using Finance.Domain.Ingestion;

namespace Finance.Infrastructure.Ingestion;

/// <summary>
/// <see cref="IRawFeedFiles"/> over the on-disk <c>Raw Data Feed/&lt;source&gt;/</c>
/// tree. Dot-files are ignored; results are ordered so a re-run is deterministic.
/// A missing source folder is treated the same as an empty one.
/// </summary>
public sealed class PhysicalRawFeedFiles : IRawFeedFiles
{
    private readonly string _root;

    public PhysicalRawFeedFiles(string root) => _root = root;

    public IReadOnlyList<RawFeedFile> List(string sourceId)
    {
        var folder = Path.Combine(_root, sourceId);
        if (!Directory.Exists(folder))
            return [];

        return Directory.EnumerateFiles(folder)
            .Where(f => !Path.GetFileName(f).StartsWith('.'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new RawFeedFile(Path.GetFileName(f), f))
            .ToList();
    }
}
