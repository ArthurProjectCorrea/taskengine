using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Windows.Graphics;

namespace TaskEngine.Desktop.Platforms.Windows;

/// <summary>
/// Controla a janela principal do app no Windows (issue #2): moldura e barra de título nativas,
/// redimensionável, tamanho inicial generoso com mínimo definido, visível na barra de
/// tarefas/Alt+Tab. Substitui a antiga barra de comando flutuante frameless estilo
/// Raycast/Spotlight (700x80, sempre no topo, fora da barra de tarefas) - o protótipo aprovado
/// (docs/prototipo/*.dc.html) é uma janela normal com uma sidebar de navegação, não uma barra de
/// comando. O hide-to-tray (esconder em vez de encerrar o processo ao fechar/perder foco) e a
/// centralização no monitor do cursor ao mostrar são mantidos idênticos ao comportamento anterior.
/// Usa interop WinRT/AppWindow por trás da <see cref="Microsoft.Maui.Controls.Window"/> do MAUI —
/// nenhum tipo específico de plataforma vaza para código compartilhado.
/// </summary>
internal static class MainWindowManager
{
    private const int InitialWindowWidth = 1100;
    private const int InitialWindowHeight = 720;
    private const int MinWindowWidth = 900;
    private const int MinWindowHeight = 600;

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
            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: true);
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
            presenter.IsAlwaysOnTop = false;
            presenter.PreferredMinimumWidth = MinWindowWidth;
            presenter.PreferredMinimumHeight = MinWindowHeight;
        }

        // Agora é uma janela normal: aparece na barra de tarefas e no Alt+Tab (issue #2 - antes
        // era uma barra de comando escondida dos dois de propósito). Mudança de comportamento
        // intencional.
        appWindow.IsShownInSwitchers = true;
        appWindow.Resize(new SizeInt32(InitialWindowWidth, InitialWindowHeight));

        appWindow.Changed += OnAppWindowChanged;
        appWindow.Closing += OnAppWindowClosing;

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

    /// <summary>
    /// Qualquer pedido nativo de fechamento da janela (ex.: Alt+F4, botão "X" da barra de título)
    /// é genérico: nunca encerra o processo por conta própria, só esconde — o único jeito de
    /// encerrar de verdade é pelo menu "Fechar" do ícone de bandeja (<see cref="TrayIconService"/>).
    /// </summary>
    private static void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        args.Cancel = true;
        Hide();
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
