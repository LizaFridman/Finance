namespace Finance.Export;

/// <summary>
/// Translates an internal bucket id to the old Daily Expenses "Split Method"
/// string, used only at export time (spec §9). Nothing in the core reads these.
/// </summary>
public static class LegacySplitMethodMap
{
    public static string ForBucket(string? bucketId) => bucketId switch
    {
        // Backwards-compatibility special-case (spec §2): the legacy value
        // "Partner 100%" is historical data meaning *Akumu owes 100%* — it has
        // nothing to do with Partner the telecom. Emitted verbatim so old-format
        // consumers keep working; never used as a naming pattern in new code.
        "personal_akumu" => "Partner 100%",

        // Best-effort labels for the other buckets. The old sheet's full set of
        // Split Method values isn't pinned by the spec, so adjust these to match
        // whatever the target file actually expects.
        "personal_liza" => "Liza 100%",
        "shared" => "50/50",
        _ => string.Empty,
    };
}
