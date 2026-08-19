using System.Drawing;
using System.Windows.Forms;

namespace TaskEngine.Desktop.Platforms.Windows;

/// <summary>
/// Ícone de bandeja do sistema (<see cref="NotifyIcon"/>, framework Windows Forms nativo,
/// habilitado via <c>UseWindowsForms</c> — não é biblioteca de UI de terceiros). Expõe os dois
/// únicos itens de menu: "Abrir painel" (mostra a janela) e "Fechar" (encerra o processo de
/// verdade) — é o único lugar do app que realmente termina o processo; qualquer outra forma de
/// "fechar" a janela (clique fora, Alt+F4) apenas a esconde.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event Action? OpenRequested;

    public event Action? ExitRequested;

    public TrayIconService()
    {
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Abrir painel", null, (_, _) => OpenRequested?.Invoke());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Fechar", null, (_, _) => ExitRequested?.Invoke());

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
