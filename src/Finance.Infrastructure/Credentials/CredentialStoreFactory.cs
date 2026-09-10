using Finance.Domain.Credentials;

namespace Finance.Infrastructure.Credentials;

/// <summary>
/// Chooses a concrete <see cref="ICredentialStore"/>. The host machine (spec §15)
/// is still undecided, so this stays a runtime choice, overridable by config.
/// </summary>
public static class CredentialStoreFactory
{
    public static ICredentialStore Create(
        CredentialStoreKind kind = CredentialStoreKind.Auto, string? dotEnvPath = null)
    {
        var resolved = kind == CredentialStoreKind.Auto
            ? (OperatingSystem.IsWindows() ? CredentialStoreKind.Windows : CredentialStoreKind.DotEnv)
            : kind;

        return resolved switch
        {
            CredentialStoreKind.DotEnv => new DotEnvCredentialStore(dotEnvPath ?? ".env"),
            CredentialStoreKind.Windows when OperatingSystem.IsWindows() => new WindowsCredentialStore(),
            CredentialStoreKind.Windows => throw new PlatformNotSupportedException(
                "The Windows credential store needs Windows. Use CredentialStoreKind.DotEnv on a Linux/Pi host."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }
}
