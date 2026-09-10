namespace Finance.Domain.Credentials;

/// <summary>
/// A source's login material. <see cref="ExtraId"/> covers sources that need a
/// third field (Max: username + password + id — spec §5).
/// </summary>
public sealed record Credential(string Username, string Secret, string? ExtraId = null);

/// <summary>
/// Local, OS-appropriate storage for bank/card credentials. Credentials never
/// leave the host machine and are never committed (spec §7.2). Concrete stores
/// live in the infrastructure ring; the port is defined here so ingestion can
/// depend on it without knowing the medium.
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
