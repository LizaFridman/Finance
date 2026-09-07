using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;

namespace Finance.Core.Credentials;

/// <summary>
/// Stores each credential as a generic entry in Windows Credential Manager under
/// the target name <c>Finance:&lt;sourceId&gt;</c>. The full <see cref="Credential"/>
/// (including secret and optional id) is serialized into the credential blob.
/// Nothing is written to disk in the repo and nothing is uploaded (spec §7.2).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialStore : ICredentialStore
{
    private readonly string _targetPrefix;

    public WindowsCredentialStore(string targetPrefix = "Finance:")
    {
        _targetPrefix = targetPrefix;
    }

    private string Target(string sourceId) => _targetPrefix + sourceId.Trim();

    public Credential? Get(string sourceId)
    {
        if (!CredRead(Target(sourceId), CRED_TYPE_GENERIC, 0, out var handle))
            return null;

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(handle);
            var json = cred.CredentialBlobSize > 0
                ? Encoding.UTF8.GetString(ReadBlob(cred.CredentialBlob, (int)cred.CredentialBlobSize))
                : "{}";
            var stored = JsonSerializer.Deserialize<StoredCredential>(json);
            return stored is null
                ? null
                : new Credential(stored.Username, stored.Secret,
                    string.IsNullOrEmpty(stored.ExtraId) ? null : stored.ExtraId);
        }
        finally
        {
            CredFree(handle);
        }
    }

    public void Set(string sourceId, Credential credential)
    {
        var blob = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new StoredCredential(
            credential.Username, credential.Secret, credential.ExtraId)));
        var blobHandle = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobHandle, blob.Length);
            var cred = new CREDENTIAL
            {
                Type = CRED_TYPE_GENERIC,
                TargetName = Target(sourceId),
                CredentialBlob = blobHandle,
                CredentialBlobSize = (uint)blob.Length,
                Persist = CRED_PERSIST_LOCAL_MACHINE,
                UserName = credential.Username,
            };
            if (!CredWrite(ref cred, 0))
                throw new InvalidOperationException(
                    $"CredWrite failed (Win32 error {Marshal.GetLastWin32Error()}).");
        }
        finally
        {
            Marshal.FreeHGlobal(blobHandle);
        }
    }

    public void Delete(string sourceId)
    {
        // Missing entry is not an error — Delete is idempotent.
        CredDelete(Target(sourceId), CRED_TYPE_GENERIC, 0);
    }

    private sealed record StoredCredential(string Username, string Secret, string? ExtraId);

    private static byte[] ReadBlob(IntPtr source, int size)
    {
        var buffer = new byte[size];
        Marshal.Copy(source, buffer, 0, size);
        return buffer;
    }

    // --- Win32 interop ------------------------------------------------------

    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern void CredFree(IntPtr buffer);
}
