using System.IO.Compression;
using System.Text.Json;
using TaskEngine.Domain.Entities;
using TaskEngine.Infrastructure.Persistence;

namespace TaskEngine.Infrastructure.Tests.Persistence;

public class SqliteBackupServiceTests
{
    [Fact]
    public async Task ExportAsync_ProducesZip_WithManifestAndDatabaseEntries()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var service = new SqliteBackupService(db.PathProvider);
        var zipPath = Path.Combine(Path.GetTempPath(), $"taskengine-backup-test-{Guid.NewGuid()}.zip");

        try
        {
            await service.ExportAsync(zipPath, CancellationToken.None);

            Assert.True(File.Exists(zipPath));

            using var archive = ZipFile.OpenRead(zipPath);
            var manifestEntry = archive.GetEntry("manifest.json");
            var databaseEntry = archive.GetEntry("taskengine.db");

            Assert.NotNull(manifestEntry);
            Assert.NotNull(databaseEntry);

            await using var manifestStream = manifestEntry!.Open();
            var manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream);

            Assert.NotNull(manifest);
            Assert.Equal(1, manifest!.SchemaVersion);
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_IncludesTaskData_ButStripsCredentialRows()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();

        var taskRepository = new SqliteTaskRepository(db.PathProvider);
        var appSettingsStore = new SqliteAppSettingsStore(db.PathProvider);

        var task = TaskItem.Create("Write report", "Quarterly summary");
        await taskRepository.AddAsync(task, CancellationToken.None);
        await appSettingsStore.SetAsync("credential:test", "super-secret-token", CancellationToken.None);

        var service = new SqliteBackupService(db.PathProvider);
        var zipPath = Path.Combine(Path.GetTempPath(), $"taskengine-backup-test-{Guid.NewGuid()}.zip");
        var extractedDbPath = Path.Combine(Path.GetTempPath(), $"taskengine-backup-test-extracted-{Guid.NewGuid()}.db");

        try
        {
            await service.ExportAsync(zipPath, CancellationToken.None);

            using (var archive = ZipFile.OpenRead(zipPath))
            {
                var databaseEntry = archive.GetEntry("taskengine.db");
                Assert.NotNull(databaseEntry);
                databaseEntry!.ExtractToFile(extractedDbPath, overwrite: true);
            }

            var extractedPathProvider = new SqlitePathProvider(extractedDbPath);
            var extractedTaskRepository = new SqliteTaskRepository(extractedPathProvider);
            var extractedAppSettingsStore = new SqliteAppSettingsStore(extractedPathProvider);

            var extractedTask = await extractedTaskRepository.GetByIdAsync(task.Id, CancellationToken.None);
            var extractedCredential = await extractedAppSettingsStore.GetAsync("credential:test", CancellationToken.None);

            Assert.NotNull(extractedTask);
            Assert.Equal(task.Title, extractedTask!.Title);
            Assert.Null(extractedCredential);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearPool(new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={extractedDbPath}"));

            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            if (File.Exists(extractedDbPath))
            {
                File.Delete(extractedDbPath);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_RestoresDataIntoDifferentDestination()
    {
        using var sourceDb = new TempSqliteDatabase();
        await sourceDb.InitializeAsync();
        var sourceTaskRepository = new SqliteTaskRepository(sourceDb.PathProvider);

        var task = TaskItem.Create("Write report", "Quarterly summary");
        await sourceTaskRepository.AddAsync(task, CancellationToken.None);

        var exportService = new SqliteBackupService(sourceDb.PathProvider);
        var zipPath = Path.Combine(Path.GetTempPath(), $"taskengine-backup-test-{Guid.NewGuid()}.zip");

        using var destinationDb = new TempSqliteDatabase();
        await destinationDb.InitializeAsync();
        var importService = new SqliteBackupService(destinationDb.PathProvider);

        try
        {
            await exportService.ExportAsync(zipPath, CancellationToken.None);
            await importService.ImportAsync(zipPath, CancellationToken.None);

            var destinationTaskRepository = new SqliteTaskRepository(destinationDb.PathProvider);
            var restoredTask = await destinationTaskRepository.GetByIdAsync(task.Id, CancellationToken.None);

            Assert.NotNull(restoredTask);
            Assert.Equal(task.Title, restoredTask!.Title);
            Assert.Equal(task.Description, restoredTask.Description);
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }
        }
    }

    [Fact]
    public async Task ImportAsync_WhenSchemaVersionIsUnsupported_ThrowsWithClearMessage()
    {
        using var db = new TempSqliteDatabase();
        await db.InitializeAsync();
        var service = new SqliteBackupService(db.PathProvider);

        var zipPath = Path.Combine(Path.GetTempPath(), $"taskengine-backup-test-{Guid.NewGuid()}.zip");
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"taskengine-backup-test-src-{Guid.NewGuid()}.db");

        try
        {
            File.WriteAllText(tempDbPath, "not a real database, just needs to exist as a zip entry");

            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(tempDbPath, "taskengine.db");

                var manifestEntry = archive.CreateEntry("manifest.json");
                await using var manifestStream = manifestEntry.Open();
                var manifest = new BackupManifest { SchemaVersion = 999, ExportedAt = DateTimeOffset.UtcNow };
                await JsonSerializer.SerializeAsync(manifestStream, manifest);
            }

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ImportAsync(zipPath, CancellationToken.None));

            Assert.Contains("999", exception.Message);
        }
        finally
        {
            if (File.Exists(zipPath))
            {
                File.Delete(zipPath);
            }

            if (File.Exists(tempDbPath))
            {
                File.Delete(tempDbPath);
            }
        }
    }
}
