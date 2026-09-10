namespace Finance.Domain.Credentials;

/// <summary>
/// Stores credentials as <c>SOURCE_USERNAME</c> / <c>SOURCE_PASSWORD</c> /
/// <c>SOURCE_ID</c> lines in a local <c>.env</c> file. The fallback store for a
/// non-Windows host (spec §14 P0.2). The file is gitignored and never uploaded.
/// </summary>
public sealed class DotEnvCredentialStore : ICredentialStore
{
    private readonly string _path;

    public DotEnvCredentialStore(string path)
    {
        _path = path;
    }

    private static string Prefix(string sourceId) => sourceId.Trim().ToUpperInvariant() + "_";

    public Credential? Get(string sourceId)
    {
        var env = Read();
        var prefix = Prefix(sourceId);
        if (!env.TryGetValue(prefix + "USERNAME", out var username) ||
            !env.TryGetValue(prefix + "PASSWORD", out var secret))
            return null;

        env.TryGetValue(prefix + "ID", out var extraId);
        return new Credential(username, secret, string.IsNullOrEmpty(extraId) ? null : extraId);
    }

    public void Set(string sourceId, Credential credential)
    {
        var env = Read();
        var prefix = Prefix(sourceId);
        env[prefix + "USERNAME"] = credential.Username;
        env[prefix + "PASSWORD"] = credential.Secret;
        if (credential.ExtraId is { Length: > 0 } id)
            env[prefix + "ID"] = id;
        else
            env.Remove(prefix + "ID");
        Write(env);
    }

    public void Delete(string sourceId)
    {
        var env = Read();
        var prefix = Prefix(sourceId);
        foreach (var suffix in new[] { "USERNAME", "PASSWORD", "ID" })
            env.Remove(prefix + suffix);
        Write(env);
    }

    private Dictionary<string, string> Read()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(_path))
            return env;

        foreach (var raw in File.ReadAllLines(_path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            env[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return env;
    }

    private void Write(IReadOnlyDictionary<string, string> env)
    {
        var lines = env.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                       .Select(kv => $"{kv.Key}={kv.Value}");
        File.WriteAllLines(_path, lines);
    }
}
