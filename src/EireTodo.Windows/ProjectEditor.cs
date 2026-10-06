using System.Windows;
using System.Windows.Controls;
using EireTodo.Core;

namespace EireTodo.Windows;

public sealed class ProjectEditor : Window
{
    private readonly TodoService service;
    private readonly ListBox list = new() { DisplayMemberPath = "DisplayName", MinHeight = 120 };
    private readonly TextBox name = new() { MaxLength = 100 };
    private readonly TextBlock error = new() { Foreground = Ui.Accent, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly Button rename;
    private readonly Button archive;
    public ProjectEditor(TodoService service)
    {
        this.service = service;
        Title = "Manage projects"; Width = 440; Height = 470; MinWidth = 380; MinHeight = 350;
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
        rename.Margin = archive.Margin = new Thickness(8, 0, 0, 0);
        actions.Children.Add(add); actions.Children.Add(rename); actions.Children.Add(archive); bottom.Children.Add(actions);
        bottom.Children.Add(error);
        bottom.Children.Add(new TextBlock { Text = "Archived projects retain their tasks and history. Select one to unarchive it.", Foreground = System.Windows.Media.Brushes.Silver, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 12) });
        var close = Ui.Button("Close", (_, _) => Close()); close.IsCancel = true; close.HorizontalAlignment = HorizontalAlignment.Right; bottom.Children.Add(close);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom); root.Children.Add(list); Content = root;
        list.SelectionChanged += (_, _) =>
        {
            var p = list.SelectedItem as Project;
            rename.IsEnabled = archive.IsEnabled = p is not null;
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
        rename.IsEnabled = archive.IsEnabled = list.SelectedItem is Project;
    }
}
