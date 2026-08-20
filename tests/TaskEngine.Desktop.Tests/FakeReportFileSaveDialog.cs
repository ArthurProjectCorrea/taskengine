using System.Text;
using TaskEngine.Desktop.ViewModels.Reports;

namespace TaskEngine.Desktop.Tests;

/// <summary>Hand-written in-memory fake for <see cref="IReportFileSaveDialog"/>, no mocking library.</summary>
public sealed class FakeReportFileSaveDialog : IReportFileSaveDialog
{
    private readonly bool _cancels;

    public FakeReportFileSaveDialog(bool cancels = false)
    {
        _cancels = cancels;
    }

    /// <summary>The <c>suggestedFileName</c> passed on the last (or only) call, if any.</summary>
    public string? RequestedFileName { get; private set; }

    public int CallCount { get; private set; }

    private NonDisposingMemoryStream? _capturedStream;

    /// <summary>
    /// The UTF-8 text written into the stream handed back by <see cref="RequestSaveStream"/>, once
    /// the caller (<c>RelatorioViewModel.ExportCsv</c>) is done writing and disposes it - readable
    /// here only because <see cref="NonDisposingMemoryStream"/> ignores that <c>Dispose</c> call
    /// instead of actually releasing the buffer.
    /// </summary>
    public string? CapturedCsv => _capturedStream is null ? null : Encoding.UTF8.GetString(_capturedStream.ToArray());

    public Stream? RequestSaveStream(string suggestedFileName)
    {
        CallCount++;
        RequestedFileName = suggestedFileName;

        if (_cancels)
        {
            return null;
        }

        _capturedStream = new NonDisposingMemoryStream();
        return _capturedStream;
    }

    /// <summary>A <see cref="MemoryStream"/> that survives <see cref="Dispose()"/> so a test can still read its buffer afterwards - real callers always dispose the stream they get from <see cref="IReportFileSaveDialog"/>.</summary>
    private sealed class NonDisposingMemoryStream : MemoryStream
    {
        protected override void Dispose(bool disposing)
        {
            // Intentionally not calling base.Dispose(disposing) - see class doc comment.
        }
    }
}
