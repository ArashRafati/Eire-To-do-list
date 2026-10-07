using System.Windows;
using System.Windows.Controls;
using EireTodo.Core;

namespace EireTodo.Windows;

public sealed class ProjectEditor : Window
{
    private readonly TodoService service;
    private readonly ListBox list = new() { ItemTemplate = Ui.DisplayTemplate("DisplayName"), MinHeight = 100 };
    private readonly TextBox name = new() { MaxLength = 100 };
    private readonly TextBlock error = new() { Foreground = Ui.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly Button rename;
    private readonly Button archive;
    private readonly Button delete;
    public ProjectEditor(TodoService service)
    {
        Ui.ApplyWindowStyle(this);
        this.service = service;
        Title = "Manage projects"; Width = 540; Height = 550; MinWidth = 460; MinHeight = 440;
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 32);
        MaxWidth = Math.Max(MinWidth, SystemParameters.WorkArea.Width - 32);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(18) };
        var bottom = new StackPanel();
        bottom.Children.Add(Ui.Label("Project name")); bottom.Children.Add(name);
        var actions = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        var add = Ui.Button("Create", (_, _) => Change(() => service.AddProject(name.Text)), true);
        rename = Ui.Button("Rename", (_, _) => { if (list.SelectedItem is Project p) Change(() => service.RenameProject(p.Id, name.Text)); });
        archive = Ui.Button("Archive", (_, _) =>
        {
            if (list.SelectedItem is not Project p) return;
            if (!p.Archived && MessageBox.Show(this, $"Archive {p.Name}?\n\nIts tasks stay available in All projects and its project view. New tasks cannot be assigned to it until it is unarchived.", "Archive project", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            Change(() => service.SetArchived(p.Id, !p.Archived));
        });
        delete = Ui.Button("Delete", (_, _) => DeleteSelected());
        delete.ToolTip = "Delete this project. Choose where to move any tasks; diagrams are preserved.";
        rename.Margin = archive.Margin = delete.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(add); actions.Children.Add(rename); actions.Children.Add(archive); actions.Children.Add(delete); bottom.Children.Add(actions);
        bottom.Children.Add(error);
        bottom.Children.Add(new TextBlock { Text = "Archive keeps a project available. Delete removes the project and moves its tasks to your chosen destination. Diagrams and task history are preserved.", Foreground = Ui.Ink, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) });
        var close = Ui.Button("Close", (_, _) => Close()); close.IsCancel = true; close.HorizontalAlignment = HorizontalAlignment.Right; bottom.Children.Add(close);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom); root.Children.Add(list); Content = root;
        TextSearch.SetTextPath(list, "DisplayName");
        list.SelectionChanged += (_, _) =>
        {
            var p = list.SelectedItem as Project;
            rename.IsEnabled = archive.IsEnabled = delete.IsEnabled = p is not null;
            archive.Content = p?.Archived == true ? "Unarchive" : "Archive";
            if (p is not null) name.Text = p.Name;
        };
        Refresh();
    }
    private void Change(Action action)
    {
        try { action(); error.Text = ""; Refresh(); }
        catch (Exception ex) { error.Text = "Project change not saved: " + ex.Message; }
    }
    private void Refresh()
    {
        var id = (list.SelectedItem as Project)?.Id;
        list.ItemsSource = service.Data.Projects.OrderBy(p => p.Archived).ThenBy(p => p.Name).ToList();
        list.SelectedItem = service.Data.Projects.FirstOrDefault(p => p.Id == id);
        rename.IsEnabled = archive.IsEnabled = delete.IsEnabled = list.SelectedItem is Project;
    }
    private void DeleteSelected()
    {
        if (list.SelectedItem is not Project project) return;
        var count = service.Data.Tasks.Count(t => t.ProjectId == project.Id);
        if (count == 0) { Change(() => service.DeleteProject(project.Id)); return; }
        var dialog = new Window { Title = "Delete project / move tasks", Owner = this, Width = 500, Height = 360, MinWidth = 420, MinHeight = 320, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Ui.ApplyWindowStyle(dialog);
        var destinations = new List<ProjectChoice> { new("Create a new destination project", null) };
        destinations.AddRange(service.Data.Projects.Where(p => p.Id != project.Id && !p.Archived).Select(p => new ProjectChoice(p.Name, p.Id)));
        var target = new ComboBox { ItemsSource = destinations, ItemTemplate = Ui.DisplayTemplate("Label"), SelectedIndex = destinations.Count > 1 ? 1 : 0 };
        var newName = new TextBox();
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Ui.Error };
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = $"Delete {project.Name} and move its {count} task(s). Task IDs, dates, notes and status will be retained.", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(Ui.Field("Destination", target)); panel.Children.Add(Ui.Field("New project name (when creating a destination)", newName)); panel.Children.Add(message);
        target.SelectionChanged += (_, _) => newName.IsEnabled = (target.SelectedItem as ProjectChoice)?.Value is null;
        newName.IsEnabled = destinations.Count == 1;
        var actions = new WrapPanel { Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = Ui.Button("Cancel", (_, _) => dialog.Close()); cancel.IsCancel = true; actions.Children.Add(cancel);
        var remove = Ui.Button("Delete project / move tasks", (_, _) =>
        {
            try
            {
                var destination = (target.SelectedItem as ProjectChoice)?.Value;
                service.DeleteProject(project.Id, destination, destination.HasValue ? null : newName.Text);
                dialog.DialogResult = true;
            }
            catch (Exception ex) { message.Text = "Project not deleted: " + ex.Message; }
        }, true); remove.Margin = new Thickness(8, 0, 0, 0); actions.Children.Add(remove); panel.Children.Add(actions);
        dialog.Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        if (dialog.ShowDialog() == true) { error.Text = ""; Refresh(); }
    }
}
