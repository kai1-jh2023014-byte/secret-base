using System.Runtime.InteropServices;
using System.Text;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Windows;

/// <summary>
/// Documented Windows Credential Manager storage (CredWrite / CredRead / CredDelete).
/// Target names are prefixed with <c>SecretBase/</c>. Never logs secret values.
/// </summary>
public sealed class WindowsCredentialSecretStore : ISecureSecretStore
{
    private const string Prefix = "SecretBase/";

    public bool TryGetSecret(string key, out string? value)
    {
        value = null;
        var target = Prefix + key;
        if (!NativeMethods.CredRead(target, NativeMethods.CredTypeGeneric, 0, out var ptr) || ptr == nint.Zero)
        {
            return false;
        }

        try
        {
            var cred = Marshal.PtrToStructure<NativeMethods.Credential>(ptr);
            if (cred.CredentialBlob == nint.Zero || cred.CredentialBlobSize == 0)
            {
                return false;
            }

            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
            value = Encoding.UTF8.GetString(bytes);
            return !string.IsNullOrEmpty(value);
        }
        finally
        {
            NativeMethods.CredFree(ptr);
        }
    }

    public void SetSecret(string key, string value)
    {
        var target = Prefix + key;
        var bytes = Encoding.UTF8.GetBytes(value);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var cred = new NativeMethods.Credential
            {
                Type = NativeMethods.CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = NativeMethods.CredPersistLocalMachine,
                UserName = "SecretBase"
            };

            if (!NativeMethods.CredWrite(ref cred, 0))
            {
                throw new InvalidOperationException("CredWrite failed.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blob);
        }
    }

    public void DeleteSecret(string key)
    {
        var target = Prefix + key;
        _ = NativeMethods.CredDelete(target, NativeMethods.CredTypeGeneric, 0);
    }

    private static class NativeMethods
    {
        public const uint CredTypeGeneric = 1;
        public const uint CredPersistLocalMachine = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct Credential
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string? Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public nint CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public nint Attributes;
            public string? TargetAlias;
            public string? UserName;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredWrite(ref Credential userCredential, uint flags);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredRead(string target, uint type, uint flags, out nint credentialPtr);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll", SetLastError = true)]
        public static extern void CredFree(nint buffer);
    }
}
