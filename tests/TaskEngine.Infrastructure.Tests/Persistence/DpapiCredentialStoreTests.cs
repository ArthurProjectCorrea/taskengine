using System.Runtime.Versioning;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

[SupportedOSPlatform("windows")]
public class DpapiCredentialStoreTests
{
    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTripsValue()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var appSettingsStore = new SqliteAppSettingsStore(db.PathProvider);
        var store = new DpapiCredentialStore(appSettingsStore);

        await store.SaveAsync("openai-api-key", "sk-super-secret-value", CancellationToken.None);
        var value = await store.GetAsync("openai-api-key", CancellationToken.None);

        Assert.Equal("sk-super-secret-value", value);
    }

    [Fact]
    public async Task SaveAsync_PersistsEncryptedValue_NotPlainText()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var appSettingsStore = new SqliteAppSettingsStore(db.PathProvider);
        var store = new DpapiCredentialStore(appSettingsStore);

        const string secret = "sk-super-secret-value";
        await store.SaveAsync("openai-api-key", secret, CancellationToken.None);

        var rawValue = await appSettingsStore.GetAsync("credential:openai-api-key", CancellationToken.None);

        Assert.NotNull(rawValue);
        Assert.NotEqual(secret, rawValue);
    }

    [Fact]
    public async Task GetAsync_WhenKeyWasNeverSaved_ReturnsNull()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var store = new DpapiCredentialStore(new SqliteAppSettingsStore(db.PathProvider));

        var value = await store.GetAsync("missing-key", CancellationToken.None);

        Assert.Null(value);
    }

    [Fact]
    public async Task DeleteAsync_RemovesValue_SoGetAsyncReturnsNull()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var store = new DpapiCredentialStore(new SqliteAppSettingsStore(db.PathProvider));

        await store.SaveAsync("openai-api-key", "sk-super-secret-value", CancellationToken.None);
        await store.DeleteAsync("openai-api-key", CancellationToken.None);
        var value = await store.GetAsync("openai-api-key", CancellationToken.None);

        Assert.Null(value);
    }

    [Fact]
    public async Task SaveAsync_ForDifferentKeys_DoesNotCollide()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var store = new DpapiCredentialStore(new SqliteAppSettingsStore(db.PathProvider));

        await store.SaveAsync("openai-api-key", "openai-secret", CancellationToken.None);
        await store.SaveAsync("anthropic-api-key", "anthropic-secret", CancellationToken.None);

        var openAiValue = await store.GetAsync("openai-api-key", CancellationToken.None);
        var anthropicValue = await store.GetAsync("anthropic-api-key", CancellationToken.None);

        Assert.Equal("openai-secret", openAiValue);
        Assert.Equal("anthropic-secret", anthropicValue);
    }
}
