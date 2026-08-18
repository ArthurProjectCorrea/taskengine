namespace TaskEngine.Infrastructure.Persistence;

/// <summary>
/// Metadata written into every backup archive's <c>manifest.json</c>. <see cref="SchemaVersion"/>
/// versions the backup file format itself (not the app version) so future
/// <see cref="SqliteBackupService"/> revisions can detect and reject archives whose format they
/// don't know how to restore.
/// </summary>
public sealed class BackupManifest
{
    public int SchemaVersion { get; set; }

    public DateTimeOffset ExportedAt { get; set; }
}
