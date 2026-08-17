using System.Drawing;
using System.Windows.Forms;

namespace TaskEngine.Desktop.Platforms.Windows;

/// <summary>
/// Ícone de bandeja do sistema (<see cref="NotifyIcon"/>, framework Windows Forms nativo,
/// habilitado via <c>UseWindowsForms</c> — não é biblioteca de UI de terceiros). Expõe os dois
/// únicos itens de menu definidos na issue: "Abrir" (mostra/esconde a janela) e "Sair" (encerra
/// o processo de verdade).
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event Action? OpenRequested;

    public event Action? ExitRequested;

    public TrayIconService()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Abrir", null, (_, _) => OpenRequested?.Invoke());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Sair", null, (_, _) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "TaskEngine",
            ContextMenuStrip = contextMenu,
            Visible = true,
        };

        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var extracted = Icon.ExtractAssociatedIcon(exePath);
                if (extracted is not null)
                {
                    return extracted;
                }
            }
        }
        catch (Exception)
        {
            // Ignora e usa o ícone nativo de fallback abaixo — a bandeja não pode falhar por isso.
        }

        return SystemIcons.Application;
    }
}
