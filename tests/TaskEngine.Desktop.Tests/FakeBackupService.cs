using TaskEngine.Application.Abstractions;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="IBackupService"/>, no mocking library.</summary>
public sealed class FakeBackupService : IBackupService
{
    private readonly Exception? _exportFailure;
    private readonly Exception? _importFailure;

    public FakeBackupService(Exception? exportFailure = null, Exception? importFailure = null)
    {
        _exportFailure = exportFailure;
        _importFailure = importFailure;
    }

    public int ExportCallCount { get; private set; }

    public int ImportCallCount { get; private set; }

    public string? LastExportedPath { get; private set; }

    public string? LastImportedPath { get; private set; }

    public Task ExportAsync(string destinationZipPath, CancellationToken cancellationToken)
    {
        ExportCallCount++;
        LastExportedPath = destinationZipPath;

        if (_exportFailure is not null)
        {
            throw _exportFailure;
        }

        return Task.CompletedTask;
    }

    public Task ImportAsync(string sourceZipPath, CancellationToken cancellationToken)
    {
        ImportCallCount++;
        LastImportedPath = sourceZipPath;

        if (_importFailure is not null)
        {
            throw _importFailure;
        }

        return Task.CompletedTask;
    }
}
