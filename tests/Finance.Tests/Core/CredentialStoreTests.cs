using System;
using System.IO;
using Finance.Core.Credentials;

namespace Finance.Tests.Core;

public class DotEnvCredentialStoreTests : IDisposable
{
    private readonly string _envPath =
        Path.Combine(Path.GetTempPath(), $"creds-{Guid.NewGuid():N}.env");

    public void Dispose()
    {
        try { File.Delete(_envPath); } catch (IOException) { }
    }

    [Fact]
    public void Unknown_source_returns_null()
    {
        Assert.Null(new DotEnvCredentialStore(_envPath).Get("leumi"));
    }

    [Fact]
    public void A_two_field_credential_round_trips()
    {
        var store = new DotEnvCredentialStore(_envPath);

        store.Set("leumi", new Credential("liza", "s3cret"));

        var back = store.Get("leumi");
        Assert.Equal("liza", back!.Username);
        Assert.Equal("s3cret", back.Secret);
        Assert.Null(back.ExtraId);
    }

    [Fact]
    public void The_third_field_used_by_Max_round_trips()
    {
        var store = new DotEnvCredentialStore(_envPath);

        store.Set("max", new Credential("liza", "pw", ExtraId: "029"));

        Assert.Equal("029", store.Get("max")!.ExtraId);
    }

    [Fact]
    public void Set_is_scoped_per_source_and_Delete_only_removes_one()
    {
        var store = new DotEnvCredentialStore(_envPath);
        store.Set("leumi", new Credential("a", "1"));
        store.Set("cal", new Credential("b", "2"));

        store.Delete("leumi");

        Assert.Null(store.Get("leumi"));
        Assert.Equal("b", store.Get("cal")!.Username);
    }
}

public class CredentialStoreFactoryTests
{
    [SkippableFact]
    public void Auto_picks_the_Windows_store_on_Windows()
    {
        Skip.IfNot(OperatingSystem.IsWindows(),
            "Windows-only: the Linux/Pi host gets DotEnv instead — asserted where it runs.");

        Assert.IsType<WindowsCredentialStore>(CredentialStoreFactory.Create());
    }

    [Fact]
    public void An_explicit_kind_overrides_the_OS_default()
    {
        Assert.IsType<DotEnvCredentialStore>(
            CredentialStoreFactory.Create(CredentialStoreKind.DotEnv, dotEnvPath: "unused.env"));
    }
}

public class WindowsCredentialStoreTests
{
    [SkippableFact]
    public void Round_trips_through_Windows_Credential_Manager()
    {
        Skip.IfNot(OperatingSystem.IsWindows(),
            "Windows-only: exercises the real Windows Credential Manager.");

        // Unique prefix + guaranteed cleanup so the dev machine's credential
        // store is left exactly as it was found.
        var store = new WindowsCredentialStore($"FinanceTest-{Guid.NewGuid():N}:");
        try
        {
            Assert.Null(store.Get("leumi"));

            store.Set("leumi", new Credential("liza", "s3cret", ExtraId: "042"));

            var back = store.Get("leumi");
            Assert.Equal("liza", back!.Username);
            Assert.Equal("s3cret", back.Secret);
            Assert.Equal("042", back.ExtraId);
        }
        finally
        {
            store.Delete("leumi");
        }

        Assert.Null(store.Get("leumi"));
    }
}
