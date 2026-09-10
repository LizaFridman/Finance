namespace Finance.Domain.Ingestion;

/// <summary>
/// One raw-feed file to ingest: its <see cref="Name"/> (for parser selection) and
/// an opaque <see cref="Path"/> handle the parser can open.
/// </summary>
public readonly record struct RawFeedFile(string Name, string Path);

/// <summary>
/// Lists the raw-feed files available for a source. Where they physically live
/// (the <c>Raw Data Feed/&lt;source&gt;/</c> tree) is an infrastructure detail; a
/// source with no folder or no files just returns an empty list (spec §7.3).
/// </summary>
public interface IRawFeedFiles
{
    IReadOnlyList<RawFeedFile> List(string sourceId);
}
