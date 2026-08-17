using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace TaskEngine.Desktop.Platforms.Windows;

/// <summary>
/// Controla a janela flutuante estilo Raycast/Spotlight no Windows: remove moldura, fixa o
/// tamanho, mantém sempre no topo e fora da barra de tarefas, e a centraliza no monitor onde
/// está o cursor sempre que ela é exibida. Usa interop WinRT/AppWindow por trás da
/// <see cref="Microsoft.Maui.Controls.Window"/> do MAUI — nenhum tipo específico de plataforma
/// vaza para código compartilhado.
/// </summary>
internal static class FloatingWindowManager
{
    private const int WindowWidth = 700;
    private const int WindowHeight = 80;

    private static Microsoft.UI.Xaml.Window? _platformWindow;
    private static AppWindow? _appWindow;
    private static bool _shouldBeVisible;

    /// <summary>
    /// Estado de visibilidade *pretendido* (controlado só por <see cref="Show"/>/<see cref="Hide"/>),
    /// não o <c>AppWindow.IsVisible</c> bruto: o host WinUI do MAUI ativa a janela principal
    /// automaticamente ao iniciar o app, sobrescrevendo o <c>Hide()</c> feito em <see cref="Initialize"/>.
    /// <see cref="OnAppWindowChanged"/> reforça esse estado contra essa reativação.
    /// </summary>
    public static bool IsVisible => _shouldBeVisible;

    /// <summary>
    /// Configura a janela nativa uma única vez, logo após ela ser criada, e a esconde
    /// imediatamente para o app iniciar residente apenas na bandeja.
    /// </summary>
    public static void Initialize(Microsoft.Maui.Controls.Window mauiWindow)
    {
        var platformWindow = (Microsoft.UI.Xaml.Window)mauiWindow.Handler!.PlatformView!;
        _platformWindow = platformWindow;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        _appWindow = appWindow;

        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // Fora da barra de tarefas e do Alt+Tab: é uma barra de comando, não uma janela comum.
        appWindow.IsShownInSwitchers = false;
        appWindow.Resize(new SizeInt32(WindowWidth, WindowHeight));

        appWindow.Changed += OnAppWindowChanged;

        _shouldBeVisible = false;
        appWindow.Hide();
    }

    /// <summary>
    /// O host WinUI do MAUI ativa a janela principal automaticamente durante o startup do app
    /// (comportamento padrão dele), o que reexibe a janela mesmo depois do <c>Hide()</c> em
    /// <see cref="Initialize"/>. Sempre que o SO tentar tornar a janela visível sem que tenhamos
    /// pedido isso via <see cref="Show"/>, escondemos de novo imediatamente.
    /// </summary>
    private static void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidVisibilityChange && sender.IsVisible && !_shouldBeVisible)
        {
            sender.Hide();
        }
    }

    /// <summary>Centraliza no monitor atual (onde está o cursor) e exibe a janela com foco.</summary>
    public static void Show()
    {
        if (_appWindow is null || _platformWindow is null)
        {
            return;
        }

        _shouldBeVisible = true;
        CenterOnCursorMonitor(_appWindow);
        _appWindow.Show();
        _platformWindow.Activate();
    }

    /// <summary>Esconde a janela sem encerrar o processo.</summary>
    public static void Hide()
    {
        _shouldBeVisible = false;
        _appWindow?.Hide();
    }

    public static void Toggle()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    private static void CenterOnCursorMonitor(AppWindow appWindow)
    {
        var displayArea = TryGetDisplayAreaFromCursor()
            ?? DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);

        var workArea = displayArea.WorkArea;
        var x = workArea.X + ((workArea.Width - appWindow.Size.Width) / 2);
        var y = workArea.Y + ((workArea.Height - appWindow.Size.Height) / 2);

        appWindow.Move(new PointInt32(x, y));
    }

    private static DisplayArea? TryGetDisplayAreaFromCursor()
    {
        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            return null;
        }

        return DisplayArea.GetFromPoint(new PointInt32(cursor.X, cursor.Y), DisplayAreaFallback.Nearest);
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out PointInt32Native lpPoint);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointInt32Native
    {
        public int X;
        public int Y;
    }
}
