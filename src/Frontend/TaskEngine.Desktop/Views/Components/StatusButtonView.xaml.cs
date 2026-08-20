using System.Windows.Input;
using TaskEngine.Desktop.ViewModels;
using DomainTaskStatus = TaskEngine.Domain.Entities.TaskStatus;

namespace TaskEngine.Desktop.Views.Components;

/// <summary>
/// Reusable "status button": shows a task's current status and, when
/// <see cref="StatusTransitionRules"/> says the status has valid transitions, a chevron that opens
/// a dropdown listing them (docs/prototipo/TaskEngine Status Botao.dc.html).
///
/// Purely presentational/"dumb": picking a dropdown option only raises
/// <see cref="StatusChangeCommand"/> with the requested target status as its parameter - it never
/// calls into TaskEngine.Application itself (e.g. StartWorkSessionUseCase, ConcludeTaskUseCase).
/// The hosting page/view model owns that decision, including opening the completion modal for the
/// InProgress -&gt; Done transition (out of scope here - this component only requests it).
/// </summary>
public partial class StatusButtonView : ContentView
{
    public static readonly BindableProperty StatusProperty = BindableProperty.Create(
        nameof(Status),
        typeof(DomainTaskStatus),
        typeof(StatusButtonView),
        DomainTaskStatus.ToDo,
        propertyChanged: OnStatusChanged);

    /// <summary>The task's current status. Drives both the button's label and the dropdown's options.</summary>
    public DomainTaskStatus Status
    {
        get => (DomainTaskStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly BindableProperty StatusChangeCommandProperty = BindableProperty.Create(
        nameof(StatusChangeCommand),
        typeof(ICommand),
        typeof(StatusButtonView));

    /// <summary>
    /// Executed with the requested target <see cref="DomainTaskStatus"/> (boxed, as the command
    /// parameter) when the user picks a dropdown option. Bind this to a command on the hosting
    /// view model - the component itself has no opinion on what a transition should do.
    /// </summary>
    public ICommand? StatusChangeCommand
    {
        get => (ICommand?)GetValue(StatusChangeCommandProperty);
        set => SetValue(StatusChangeCommandProperty, value);
    }

    public StatusButtonView()
    {
        InitializeComponent();
        Refresh();
    }

    private static void OnStatusChanged(BindableObject bindable, object oldValue, object newValue)
    {
        ((StatusButtonView)bindable).Refresh();
    }

    private void Refresh()
    {
        MainButton.Text = StatusTransitionRules.GetStatusLabel(Status);

        var validTransitions = StatusTransitionRules.GetValidTransitions(Status);
        ChevronButton.IsVisible = validTransitions.Count > 0;
        MenuPanel.IsVisible = false;

        OptionsHost.Children.Clear();
        foreach (var targetStatus in validTransitions)
        {
            var optionButton = new Button
            {
                Text = StatusTransitionRules.GetTransitionActionLabel(Status, targetStatus),
                Style = (Style)Resources["StatusOptionButtonStyle"],
            };
            optionButton.Clicked += (_, _) => SelectOption(targetStatus);
            OptionsHost.Children.Add(optionButton);
        }
    }

    private void OnToggleMenuClicked(object? sender, EventArgs e)
    {
        if (!ChevronButton.IsVisible)
        {
            return;
        }

        MenuPanel.IsVisible = !MenuPanel.IsVisible;
    }

    private void SelectOption(DomainTaskStatus targetStatus)
    {
        MenuPanel.IsVisible = false;

        if (StatusChangeCommand?.CanExecute(targetStatus) == true)
        {
            StatusChangeCommand.Execute(targetStatus);
        }
    }
}
