namespace Finance.Domain.Credentials;

/// <summary>
/// A source's login material. <see cref="ExtraId"/> covers sources that need a
/// third field (Max: username + password + id — spec §5).
/// </summary>
public sealed record Credential(string Username, string Secret, string? ExtraId = null);

/// <summary>
/// Local, OS-appropriate storage for bank/card credentials. Credentials never
/// leave the host machine and are never committed (spec §7.2). The C# ingestion
/// orchestrator is the single owner: it reads from here and injects the values
/// into the Node scraper child process as environment variables — the scraper
/// itself has no credential store of its own.
/// </summary>
public interface ICredentialStore
{
    Credential? Get(string sourceId);
    void Set(string sourceId, Credential credential);
    void Delete(string sourceId);
}

public enum CredentialStoreKind
{
    /// <summary>Pick automatically from the current OS.</summary>
    Auto,

    /// <summary>Windows Credential Manager.</summary>
    Windows,

    /// <summary>A local <c>.env</c> file (Linux / Raspberry&#160;Pi host — spec §14 P0.2).</summary>
    DotEnv,
}

/// <summary>
/// Chooses a concrete <see cref="ICredentialStore"/>. The host machine (spec §15)
/// is still undecided, so this stays a runtime choice, overridable by config.
/// </summary>
public static class CredentialStoreFactory
{
    public static ICredentialStore Create(CredentialStoreKind kind = CredentialStoreKind.Auto, string? dotEnvPath = null)
    {
        var resolved = kind == CredentialStoreKind.Auto
            ? (OperatingSystem.IsWindows() ? CredentialStoreKind.Windows : CredentialStoreKind.DotEnv)
            : kind;

        if (resolved == CredentialStoreKind.DotEnv)
            return new DotEnvCredentialStore(dotEnvPath ?? ".env");

        if (resolved == CredentialStoreKind.Windows)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException(
                    "The Windows credential store needs Windows. Use CredentialStoreKind.DotEnv on a Linux/Pi host.");
            return new WindowsCredentialStore();
        }

        throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
    }
}
