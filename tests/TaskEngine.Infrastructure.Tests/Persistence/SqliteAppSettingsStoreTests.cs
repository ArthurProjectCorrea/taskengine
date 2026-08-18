using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class SqliteAppSettingsStoreTests
{
    [Fact]
    public async Task GetAsync_WhenKeyIsAbsent_ReturnsNull()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var store = new SqliteAppSettingsStore(db.PathProvider);

        var value = await store.GetAsync("missing-key", CancellationToken.None);

        Assert.Null(value);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTripsValue()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var store = new SqliteAppSettingsStore(db.PathProvider);

        await store.SetAsync("theme", "dark", CancellationToken.None);
        var value = await store.GetAsync("theme", CancellationToken.None);

        Assert.Equal("dark", value);
    }

    [Fact]
    public async Task SetAsync_WhenKeyAlreadyExists_OverwritesValue()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var store = new SqliteAppSettingsStore(db.PathProvider);

        await store.SetAsync("theme", "dark", CancellationToken.None);
        await store.SetAsync("theme", "light", CancellationToken.None);
        var value = await store.GetAsync("theme", CancellationToken.None);

        Assert.Equal("light", value);
    }

    [Fact]
    public async Task DeleteAsync_RemovesValue_SoGetAsyncReturnsNull()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var store = new SqliteAppSettingsStore(db.PathProvider);

        await store.SetAsync("theme", "dark", CancellationToken.None);
        await store.DeleteAsync("theme", CancellationToken.None);
        var value = await store.GetAsync("theme", CancellationToken.None);

        Assert.Null(value);
    }

    [Fact]
    public async Task DeleteAsync_WhenKeyIsAbsent_DoesNotThrow()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var store = new SqliteAppSettingsStore(db.PathProvider);

        await store.DeleteAsync("missing-key", CancellationToken.None);
    }
}
