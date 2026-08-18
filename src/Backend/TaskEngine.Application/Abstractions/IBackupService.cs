namespace TaskEngine.Application.Abstractions;

/// <summary>
/// Port for exporting the local database to a portable, self-contained backup archive and
/// restoring one back. Implemented by TaskEngine.Infrastructure (issue #37).
/// </summary>
public interface IBackupService
{
    Task ExportAsync(string destinationZipPath, CancellationToken cancellationToken);

    Task ImportAsync(string sourceZipPath, CancellationToken cancellationToken);
}
