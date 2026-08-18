using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TaskEngine.Application.Abstractions;

namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// <see cref="IBackupService"/> implementation backed by SQLite. Export produces a self-contained,
/// consistent copy of the database via <c>VACUUM INTO</c> (a native SQLite command purpose-built
/// for this) rather than a raw <see cref="File.Copy"/> of a "live" database file, which could race
/// with in-progress writes or separate WAL/SHM sidecar files. The copy then has any DPAPI-encrypted
/// credential rows stripped out (see <see cref="DpapiCredentialStore"/>) - those are undecryptable
/// on another machine/user anyway - before being zipped up with a manifest describing the backup
/// format version. Import validates that manifest before overwriting the local database.
/// </summary>
public sealed class SqliteBackupService : IBackupService
{
    /// <summary>
    /// Version of the backup archive format (entry names, manifest shape), not of the app or of
    /// the SQLite schema. Bump only when the archive format itself changes.
    /// </summary>
    private const int CurrentSchemaVersion = 1;

    private const string DatabaseEntryName = "taskengine.db";
    private const string ManifestEntryName = "manifest.json";

    private readonly SqlitePathProvider _pathProvider;

    public SqliteBackupService(SqlitePathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task ExportAsync(string destinationZipPath, CancellationToken cancellationToken)
    {
        var tempCopyPath = Path.Combine(Path.GetTempPath(), $"taskengine-backup-{Guid.NewGuid()}.db");
        try
        {
            await VacuumIntoAsync(tempCopyPath, cancellationToken);
            await StripCredentialsAsync(tempCopyPath, cancellationToken);

            // Microsoft.Data.Sqlite pools native connections by connection string, keeping the
            // temp copy's file handle open after disposal. Clear the pool so the copy can be
            // freely read below and deleted in the finally block.
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={tempCopyPath}"));

            var manifest = new BackupManifest
            {
                SchemaVersion = CurrentSchemaVersion,
                ExportedAt = DateTimeOffset.UtcNow,
            };

            await using var zipStream = new FileStream(destinationZipPath, FileMode.Create, FileAccess.Write);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            var databaseEntry = archive.CreateEntry(DatabaseEntryName, CompressionLevel.Optimal);
            await using (var entryStream = databaseEntry.Open())
            await using (var fileStream = new FileStream(tempCopyPath, FileMode.Open, FileAccess.Read))
            {
                await fileStream.CopyToAsync(entryStream, cancellationToken);
            }

            var manifestEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
            await using (var manifestStream = manifestEntry.Open())
            {
                await JsonSerializer.SerializeAsync(manifestStream, manifest, cancellationToken: cancellationToken);
            }
        }
        finally
        {
            if (File.Exists(tempCopyPath))
            {
                File.Delete(tempCopyPath);
            }
        }
    }

    public async Task ImportAsync(string sourceZipPath, CancellationToken cancellationToken)
    {
        var tempExtractDirectory = Path.Combine(Path.GetTempPath(), $"taskengine-restore-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempExtractDirectory);
        try
        {
            ZipFile.ExtractToDirectory(sourceZipPath, tempExtractDirectory);

            var manifestPath = Path.Combine(tempExtractDirectory, ManifestEntryName);
            BackupManifest? manifest;
            await using (var manifestStream = new FileStream(manifestPath, FileMode.Open, FileAccess.Read))
            {
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, cancellationToken: cancellationToken);
            }

            if (manifest is null || manifest.SchemaVersion != CurrentSchemaVersion)
            {
                var foundVersion = manifest is null ? "unknown" : manifest.SchemaVersion.ToString();
                throw new InvalidOperationException(
                    $"Cannot restore this backup: it uses schema version '{foundVersion}', but this " +
                    $"version of TaskEngine only supports schema version {CurrentSchemaVersion}.");
            }

            // Same reasoning as in ExportAsync: release any pooled handle on the destination
            // database file before overwriting it.
            SqliteConnection.ClearPool(new SqliteConnection(_pathProvider.ConnectionString));

            var extractedDatabasePath = Path.Combine(tempExtractDirectory, DatabaseEntryName);
            File.Copy(extractedDatabasePath, _pathProvider.DatabasePath, overwrite: true);
        }
        finally
        {
            Directory.Delete(tempExtractDirectory, recursive: true);
        }
    }

    private async Task VacuumIntoAsync(string destinationPath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(_pathProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "VACUUM INTO $path;";
        command.Parameters.AddWithValue("$path", destinationPath);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task StripCredentialsAsync(string databasePath, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM app_settings WHERE key LIKE 'credential:%';";

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
