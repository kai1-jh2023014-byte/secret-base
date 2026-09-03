using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using SecretBase.Platform.Abstractions;

namespace SecretBase.Platform.Mac;

/// <summary>
/// macOS Keychain for secrets when running on macOS; otherwise a user-only file store
/// (used by tests on Linux). Never logs secret values.
/// </summary>
public sealed class MacSecureSecretStore : ISecureSecretStore
{
    public const string KeychainService = "SecretBase";

    private readonly ISecureSecretStore _inner;

    public MacSecureSecretStore(string? fileDirectory = null)
    {
        _inner = OperatingSystem.IsMacOS()
            ? new KeychainSecretStore()
            : new FileBackedSecretStore(fileDirectory);
    }

    internal MacSecureSecretStore(ISecureSecretStore inner)
    {
        _inner = inner;
    }

    public bool TryGetSecret(string key, out string? value) => _inner.TryGetSecret(key, out value);

    public void SetSecret(string key, string value) => _inner.SetSecret(key, value);

    public void DeleteSecret(string key) => _inner.DeleteSecret(key);
}

/// <summary>
/// Restricts secrets to a 0600 file under AppData. Not a Keychain replacement — used when
/// Security.framework is unavailable (tests / non-macOS).
/// </summary>
internal sealed class FileBackedSecretStore : ISecureSecretStore
{
    private readonly string _directory;
    private readonly object _gate = new();

    public FileBackedSecretStore(string? directory = null)
    {
        _directory = directory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SecretBase",
                "secrets");
        Directory.CreateDirectory(_directory);
    }

    public bool TryGetSecret(string key, out string? value)
    {
        value = null;
        lock (_gate)
        {
            var path = GetPath(key);
            if (!File.Exists(path))
            {
                return false;
            }

            value = File.ReadAllText(path);
            return !string.IsNullOrEmpty(value);
        }
    }

    public void SetSecret(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            var path = GetPath(key);
            var temp = path + ".tmp";
            File.WriteAllText(temp, value);
            TryRestrictOwnerReadWrite(temp);
            File.Copy(temp, path, overwrite: true);
            TryRestrictOwnerReadWrite(path);
            File.Delete(temp);
        }
    }

    public void DeleteSecret(string key)
    {
        lock (_gate)
        {
            var path = GetPath(key);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private string GetPath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..40];
        return Path.Combine(_directory, hash + ".secret");
    }

    private static void TryRestrictOwnerReadWrite(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception)
        {
            // Best-effort; directory ACLs still apply.
        }
    }
}

internal sealed class KeychainSecretStore : ISecureSecretStore
{
    public bool TryGetSecret(string key, out string? value)
    {
        value = null;
        var account = Encoding.UTF8.GetBytes(key);
        var service = Encoding.UTF8.GetBytes(MacSecureSecretStore.KeychainService);
        var status = NativeMethods.SecKeychainFindGenericPassword(
            nint.Zero,
            (uint)service.Length,
            service,
            (uint)account.Length,
            account,
            out var length,
            out var data,
            out var item);

        try
        {
            if (status != 0 || data == nint.Zero || length == 0)
            {
                return false;
            }

            var bytes = new byte[length];
            Marshal.Copy(data, bytes, 0, bytes.Length);
            value = Encoding.UTF8.GetString(bytes);
            return !string.IsNullOrEmpty(value);
        }
        finally
        {
            if (data != nint.Zero)
            {
                _ = NativeMethods.SecKeychainItemFreeContent(nint.Zero, data);
            }

            if (item != nint.Zero)
            {
                NativeMethods.CFRelease(item);
            }
        }
    }

    public void SetSecret(string key, string value)
    {
        var account = Encoding.UTF8.GetBytes(key);
        var service = Encoding.UTF8.GetBytes(MacSecureSecretStore.KeychainService);
        var password = Encoding.UTF8.GetBytes(value);

        var find = NativeMethods.SecKeychainFindGenericPassword(
            nint.Zero,
            (uint)service.Length,
            service,
            (uint)account.Length,
            account,
            out _,
            out var data,
            out var item);

        if (data != nint.Zero)
        {
            _ = NativeMethods.SecKeychainItemFreeContent(nint.Zero, data);
        }

        if (find == 0 && item != nint.Zero)
        {
            try
            {
                var update = NativeMethods.SecKeychainItemModifyAttributesAndData(
                    item,
                    nint.Zero,
                    (uint)password.Length,
                    password);
                if (update != 0)
                {
                    throw new InvalidOperationException("Keychain update failed.");
                }
            }
            finally
            {
                NativeMethods.CFRelease(item);
            }

            return;
        }

        var add = NativeMethods.SecKeychainAddGenericPassword(
            nint.Zero,
            (uint)service.Length,
            service,
            (uint)account.Length,
            account,
            (uint)password.Length,
            password,
            out var created);
        if (created != nint.Zero)
        {
            NativeMethods.CFRelease(created);
        }

        if (add != 0)
        {
            throw new InvalidOperationException("Keychain add failed.");
        }
    }

    public void DeleteSecret(string key)
    {
        var account = Encoding.UTF8.GetBytes(key);
        var service = Encoding.UTF8.GetBytes(MacSecureSecretStore.KeychainService);
        var status = NativeMethods.SecKeychainFindGenericPassword(
            nint.Zero,
            (uint)service.Length,
            service,
            (uint)account.Length,
            account,
            out _,
            out var data,
            out var item);

        if (data != nint.Zero)
        {
            _ = NativeMethods.SecKeychainItemFreeContent(nint.Zero, data);
        }

        if (status != 0 || item == nint.Zero)
        {
            return;
        }

        try
        {
            _ = NativeMethods.SecKeychainItemDelete(item);
        }
        finally
        {
            NativeMethods.CFRelease(item);
        }
    }

    private static class NativeMethods
    {
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [DllImport(Security)]
        public static extern int SecKeychainFindGenericPassword(
            nint keychainOrArray,
            uint serviceNameLength,
            byte[] serviceName,
            uint accountNameLength,
            byte[] accountName,
            out uint passwordLength,
            out nint passwordData,
            out nint itemRef);

        [DllImport(Security)]
        public static extern int SecKeychainAddGenericPassword(
            nint keychain,
            uint serviceNameLength,
            byte[] serviceName,
            uint accountNameLength,
            byte[] accountName,
            uint passwordLength,
            byte[] passwordData,
            out nint itemRef);

        [DllImport(Security)]
        public static extern int SecKeychainItemModifyAttributesAndData(
            nint itemRef,
            nint attrList,
            uint length,
            byte[] data);

        [DllImport(Security)]
        public static extern int SecKeychainItemDelete(nint itemRef);

        [DllImport(Security)]
        public static extern int SecKeychainItemFreeContent(nint attrList, nint data);

        [DllImport(CoreFoundation)]
        public static extern void CFRelease(nint cf);
    }
}
