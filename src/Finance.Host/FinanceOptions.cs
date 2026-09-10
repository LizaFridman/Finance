using System.ComponentModel.DataAnnotations;

namespace Finance.Host;

/// <summary>
/// The <c>Finance</c> configuration section, validated at startup
/// (<c>ValidateOnStart</c>) so a bad deployment fails loudly rather than at the
/// first request.
/// </summary>
public sealed class FinanceOptions
{
    public const string SectionName = "Finance";

    /// <summary>SQLite file path; relative paths resolve against the content root.</summary>
    [Required(AllowEmptyStrings = false)]
    public string DatabasePath { get; init; } = "finance.db";

    /// <summary>Root of the <c>Raw Data Feed/&lt;source&gt;/</c> tree; relative paths resolve against the content root.</summary>
    [Required(AllowEmptyStrings = false)]
    public string RawDataFeedPath { get; init; } = "Raw Data Feed";

    /// <summary>Browser origins allowed to call the API. Empty = allow any (LAN/dev).</summary>
    public string[] DashboardOrigins { get; init; } = [];
}
