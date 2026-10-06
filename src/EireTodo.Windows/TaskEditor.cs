using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EireTodo.Core;

namespace EireTodo.Windows;

public sealed class TaskEditor : Window
{
    private readonly TodoService service;
    private readonly TodoTask? original;
    private readonly ComboBox project = new() { DisplayMemberPath = "DisplayName", SelectedValuePath = "Id" };
    private readonly TextBox description = new() { MaxLength = 10000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 56, MaxHeight = 100, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBox start = new();
    private readonly TextBox finish = new();
    private readonly ComboBox category = new() { IsEditable = true, IsTextSearchEnabled = true, MaxDropDownHeight = 200 };
    private readonly TextBox notes = new() { AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, Height = 150, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 250000 };
    private readonly CheckBox completed = new() { Content = "Completed", Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock error = new() { Foreground = Ui.Accent, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 8) };

    public TaskEditor(TodoService service, TodoTask? task, Guid? preferredProject)
    {
        this.service = service; original = task;
        Title = task is null ? "Add task" : "Edit task / full notes";
        Width = 530; Height = 685; MinWidth = 400; MinHeight = 380;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(18) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = Ui.Button("Cancel", (_, _) => Close()); cancel.IsCancel = true;
        var save = Ui.Button("Save task", Save, primary: true); save.Margin = new Thickness(8, 0, 0, 0); save.IsDefault = true;
        footer.Children.Add(cancel); footer.Children.Add(save);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var panel = new StackPanel();
        panel.Children.Add(Ui.Label("Project *")); panel.Children.Add(project);
        panel.Children.Add(Ui.Label("Task description *")); panel.Children.Add(description);
        var dates = new Grid(); dates.ColumnDefinitions.Add(new()); dates.ColumnDefinitions.Add(new());
        var startPanel = Ui.Field("Start date · dd/MM/yyyy (optional)", start); startPanel.Margin = new Thickness(0, 0, 6, 0);
        var finishPanel = Ui.Field("Finish date · dd/MM/yyyy (optional)", finish); finishPanel.Margin = new Thickness(6, 0, 0, 0); Grid.SetColumn(finishPanel, 1);
        dates.Children.Add(startPanel); dates.Children.Add(finishPanel); panel.Children.Add(dates);
        panel.Children.Add(Ui.Label("Category · choose or type (optional)")); panel.Children.Add(category);
        panel.Children.Add(Ui.Label("Notes (optional) · Ctrl+Enter to save")); panel.Children.Add(notes);
        panel.Children.Add(Ui.Label("Created date/time · read-only"));
        var created = new TextBox { IsReadOnly = true, Text = task is null ? "Recorded automatically when saved" : AustralianDates.Format(task.CreatedAt), IsTabStop = false };
        panel.Children.Add(created); panel.Children.Add(completed); panel.Children.Add(error);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        var projects = service.Data.Projects.Where(p => !p.Archived || p.Id == task?.ProjectId).OrderBy(p => p.Archived).ThenBy(p => p.Name).ToList();
        project.ItemsSource = projects;
        project.SelectedItem = projects.FirstOrDefault(p => p.Id == (task?.ProjectId ?? preferredProject)) ?? projects.FirstOrDefault();
        category.ItemsSource = service.Data.Categories.Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        if (task is not null)
        {
            description.Text = task.Description; start.Text = AustralianDates.Format(task.StartDate); finish.Text = AustralianDates.Format(task.FinishDate);
            category.Text = task.Category; notes.Text = task.Notes; completed.IsChecked = task.Completed;
        }
        Loaded += (_, _) => { description.Focus(); description.CaretIndex = description.Text.Length; };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Save(this, new RoutedEventArgs()); e.Handled = true; } };
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        try
        {
            if (project.SelectedItem is not Project selected) throw new ArgumentException("Choose a project.");
            var task = new TodoTask
            {
                ProjectId = selected.Id, Description = description.Text, StartDate = AustralianDates.ParseOptional(start.Text),
                FinishDate = AustralianDates.ParseOptional(finish.Text), Category = category.Text, Notes = notes.Text,
                Completed = completed.IsChecked == true, CreatedAt = original?.CreatedAt ?? DateTimeOffset.Now
            };
            service.SaveTask(task, original?.Id);
            DialogResult = true;
        }
        catch (Exception ex) { error.Text = "Task not saved: " + ex.Message; error.BringIntoView(); }
    }
}

internal static class Ui
{
    internal static readonly System.Windows.Media.Brush Accent = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 204, 51));
    internal static TextBlock Label(string text) => new() { Text = text, Margin = new Thickness(0, 12, 0, 5), TextWrapping = TextWrapping.Wrap };
    internal static StackPanel Field(string label, Control control) { var panel = new StackPanel(); panel.Children.Add(Label(label)); panel.Children.Add(control); return panel; }
    internal static Button Button(string text, RoutedEventHandler click, bool primary = false)
    {
        var button = new Button { Content = text };
        if (primary) button.Style = (Style)Application.Current.FindResource("PrimaryButton");
        button.Click += click; return button;
    }
}
