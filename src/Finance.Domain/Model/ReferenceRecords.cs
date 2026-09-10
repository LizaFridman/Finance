namespace Finance.Domain.Model;

/// <summary>A spending bucket. The set (3 today) is a reference table, not an enum (spec §3/§4.2).</summary>
public sealed record Bucket(string Id, string Label);

/// <summary>A spending category. Starts empty; grows from confirmed Layer 2 decisions (spec §10).</summary>
public sealed record Category(string Id, string Label);

/// <summary>
/// Lifecycle state of a data source. Unlike the source <em>list</em>, this is a
/// genuinely closed set — a source either feeds the pipeline or it doesn't — so
/// an enum is appropriate here (spec §6).
/// </summary>
public enum SourceStatus
{
    Active,
    Dormant,
}

/// <summary>A registered data source (bank, card issuer). See <see cref="Sources.SourceRegistry"/>.</summary>
public sealed record Source(string Id, SourceStatus Status);
