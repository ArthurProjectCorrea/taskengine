using TaskEngine.Desktop.ViewModels.LocalBackup;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="IBackupFileDialog"/>, no mocking library.</summary>
public sealed class FakeBackupFileDialog : IBackupFileDialog
{
    private readonly string? _exportPath;
    private readonly string? _importPath;

    public FakeBackupFileDialog(string? exportPath = "C:\\backups\\taskengine-backup.zip", string? importPath = "C:\\backups\\source.zip")
    {
        _exportPath = exportPath;
        _importPath = importPath;
    }

    public int RequestExportPathCallCount { get; private set; }

    public int RequestImportPathCallCount { get; private set; }

    public string? RequestedExportFileName { get; private set; }

    public string? RequestExportPath(string suggestedFileName)
    {
        RequestExportPathCallCount++;
        RequestedExportFileName = suggestedFileName;
        return _exportPath;
    }

    public string? RequestImportPath()
    {
        RequestImportPathCallCount++;
        return _importPath;
    }
}
