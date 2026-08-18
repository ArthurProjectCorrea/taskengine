using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using TaskEngine.Application.Abstractions;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// <see cref="ICredentialStore"/> implementation backed by Windows DPAPI
/// (<see cref="ProtectedData"/>, <see cref="DataProtectionScope.CurrentUser"/>). Secrets are
/// encrypted client-side and the resulting ciphertext (base64-encoded) is persisted through
/// <see cref="IAppSettingsStore"/>, reusing the <c>app_settings</c> table from issue #16 under a
/// namespaced key so it never collides with non-secret settings stored there.
/// </summary>
/// <remarks>
/// DPAPI is Windows-only. Calling <see cref="SaveAsync"/> or <see cref="GetAsync"/> on another
/// platform lets <see cref="PlatformNotSupportedException"/> propagate rather than masking it -
/// TaskEngine is Windows-first and a silent failure here would be worse than a loud one.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialStore : ICredentialStore
{
    private const string KeyPrefix = "credential:";

    private readonly IAppSettingsStore _appSettingsStore;

    public DpapiCredentialStore(IAppSettingsStore appSettingsStore)
    {
        _appSettingsStore = appSettingsStore;
    }

    public async Task SaveAsync(string key, string secret, CancellationToken cancellationToken)
    {
        var plainBytes = Encoding.UTF8.GetBytes(secret);
        var protectedBytes = ProtectedData.Protect(plainBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        var encoded = Convert.ToBase64String(protectedBytes);

        await _appSettingsStore.SetAsync(NamespacedKey(key), encoded, cancellationToken);
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var encoded = await _appSettingsStore.GetAsync(NamespacedKey(key), cancellationToken);
        if (encoded is null)
        {
            return null;
        }

        var protectedBytes = Convert.FromBase64String(encoded);
        var plainBytes = ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        await _appSettingsStore.DeleteAsync(NamespacedKey(key), cancellationToken);
    }

    private static string NamespacedKey(string key) => KeyPrefix + key;
}
