namespace TaskEngine.Desktop.Views;

/// <summary>
/// Trivial stand-in for the real Dashboard/Tarefas/Configurações views (Fase E). Exists only to
/// prove the shell's navigation mechanism (issue #2) actually swaps content in the sidebar's
/// content area - no view model, no business logic.
/// </summary>
public partial class PlaceholderView : ContentView
{
    public PlaceholderView(string title)
    {
        InitializeComponent();
        TitleLabel.Text = title;
    }
}
