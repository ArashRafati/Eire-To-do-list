using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed class DiagramEditor : Window
{
    public DiagramEditor(TodoService service, Diagram candidate, Action<Diagram> save)
    {
        Ui.ApplyWindowStyle(this); Title = "Diagram settings"; Width = 500; Height = 445; MinWidth = 430; MinHeight = 350;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 32);
        var name = new TextBox { Text = candidate.Name, MaxLength = 100 };
        var project = new ComboBox { ItemTemplate = Ui.DisplayTemplate("Label") };
        var options = new List<ProjectChoice> { new("No project", null) }; options.AddRange(service.Data.Projects.Select(p => new ProjectChoice(p.DisplayName, p.Id)));
        project.ItemsSource = options; project.SelectedItem = options.FirstOrDefault(p => p.Value == candidate.ProjectId) ?? options[0];
        var start = new TextBox { Text = AustralianDates.Format(candidate.ScheduleStart) };
        var error = new TextBlock { Foreground = Ui.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
        var panel = new StackPanel(); panel.Children.Add(Ui.Field("Diagram name *", name)); panel.Children.Add(Ui.Field("Project (optional)", project)); if (candidate.Kind == DiagramKind.Hierarchy) panel.Children.Add(Ui.Field("Scheduling start · dd/MM/yyyy *", start));
        panel.Children.Add(new TextBlock { Text = candidate.Kind == DiagramKind.Network ? "Nodes have independent positions and can have multiple incoming and outgoing leads." : "Undated leaf nodes use this date when exported to planning tools. Exports use a seven-day, eight-hour calendar.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0), FontSize = 15 }); panel.Children.Add(error);
        var root = new DockPanel { Margin = new Thickness(18) }; var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = Ui.Button("Cancel", (_, _) => Close()); cancel.IsCancel = true; footer.Children.Add(cancel);
        var button = Ui.Button("Save diagram", (_, _) =>
        {
            try { var oldName = candidate.Name; candidate.Name = name.Text.Trim(); if (candidate.Nodes.Count > 0 && candidate.Nodes[0].Title == oldName) candidate.Nodes[0].Title = candidate.Name; candidate.ProjectId = (project.SelectedItem as ProjectChoice)?.Value; candidate.ScheduleStart = AustralianDates.ParseOptional(start.Text) ?? throw new ArgumentException("Enter a scheduling start date."); save(candidate); DialogResult = true; }
            catch (Exception ex) { error.Text = "Diagram not saved: " + ex.Message; }
        }, true); button.Margin = new Thickness(8, 0, 0, 0); button.IsDefault = true; footer.Children.Add(button); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        Loaded += (_, _) => { name.Focus(); name.SelectAll(); };
    }
}

internal sealed class NodeEditor : Window
{
    public NodeEditor(Diagram candidate, Guid id, Action<Diagram> save)
    {
        Ui.ApplyWindowStyle(this); var node = candidate.Nodes.Single(n => n.Id == id);
        Title = "Edit node / full notes"; Width = 620; Height = 700; MinWidth = 440; MinHeight = 430; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 32);
        var label = new TextBox { Text = node.Title, MaxLength = 100 }; var start = new TextBox { Text = AustralianDates.Format(node.StartDate) }; var finish = new TextBox { Text = AustralianDates.Format(node.FinishDate) };
        var duration = new TextBox { Text = node.DurationDays.ToString(CultureInfo.InvariantCulture) }; var notes = new TextBox { Text = node.Notes, AcceptsReturn = true, AcceptsTab = true, TextWrapping = TextWrapping.Wrap, Height = 155, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 250000 };
        var completed = new CheckBox { Content = "Completed", IsChecked = node.Completed, Margin = new Thickness(0, 16, 0, 0) };
        var priority = new CheckBox { Content = "Priority", IsChecked = node.Priority, Margin = new Thickness(0, 8, 0, 0) };
        var parent = new ComboBox { ItemTemplate = Ui.DisplayTemplate("Label") }; var forbidden = Charts.Descendants(candidate, id);
        var parents = new List<ProjectChoice> { new("Top level (no parent)", null) };
        parents.AddRange(Charts.Outline(candidate).Where(o => !forbidden.Contains(o.Node.Id)).Select(o => new ProjectChoice(o.Code + " · " + o.Node.Title, o.Node.Id)));
        parent.ItemsSource = parents; parent.SelectedItem = parents.First(p => p.Value == node.ParentId);
        var error = new TextBlock { Foreground = Ui.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
        var panel = new StackPanel(); panel.Children.Add(Ui.Field("Node label * · up to 100 characters", label)); if (candidate.Kind == DiagramKind.Hierarchy) panel.Children.Add(Ui.Field("Parent · moves this branch", parent));
        var dates = new Grid(); dates.ColumnDefinitions.Add(new()); dates.ColumnDefinitions.Add(new());
        var a = Ui.Field("Start · dd/MM/yyyy (optional)", start); a.Margin = new Thickness(0, 0, 6, 0); var z = Ui.Field("Finish · dd/MM/yyyy (optional)", finish); z.Margin = new Thickness(6, 0, 0, 0); Grid.SetColumn(z, 1); dates.Children.Add(a); dates.Children.Add(z); panel.Children.Add(dates);
        if (candidate.Kind == DiagramKind.Hierarchy) panel.Children.Add(Ui.Field("Duration · days (1–3,650)", duration));
        panel.Children.Add(new TextBlock { Text = candidate.Kind == DiagramKind.Network ? "Dates are optional. Unfinished nodes with a finish date before today appear in the overdue log." : "For leaf nodes, both dates override duration. Branch dates roll up from children in planning exports. Diagram links show hierarchy, not scheduling dependencies.", TextWrapping = TextWrapping.Wrap, FontSize = 15, Margin = new Thickness(0, 12, 0, 0) });
        panel.Children.Add(Ui.Field("Full notes · Ctrl+Enter to save", notes)); panel.Children.Add(completed); panel.Children.Add(priority); panel.Children.Add(Ui.Label(candidate.Kind == DiagramKind.Network ? "Permanent node ID · " + node.Id : "Permanent export ID · " + node.ActivityId)); panel.Children.Add(error);
        void Commit()
        {
            try
            {
                var changed = candidate.Clone(); var n = changed.Nodes.Single(n => n.Id == id); n.Title = label.Text.Trim(); n.Notes = notes.Text; n.StartDate = AustralianDates.ParseOptional(start.Text); n.FinishDate = AustralianDates.ParseOptional(finish.Text); n.Completed = completed.IsChecked == true; n.Priority = priority.IsChecked == true;
                if (!int.TryParse(duration.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var days)) throw new ArgumentException("Enter duration as a whole number of days."); n.DurationDays = days;
                var parentId = (parent.SelectedItem as ProjectChoice)?.Value;
                if (n.ParentId != parentId) Charts.Move(changed, id, parentId); Charts.Validate(changed); save(changed); DialogResult = true;
            }
            catch (Exception ex) { error.Text = "Node not saved: " + ex.Message; error.BringIntoView(); }
        }
        var root = new DockPanel { Margin = new Thickness(18) }; var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = Ui.Button("Cancel", (_, _) => Close()); cancel.IsCancel = true; footer.Children.Add(cancel); var button = Ui.Button("Save node", (_, _) => Commit(), true); button.Margin = new Thickness(8, 0, 0, 0); button.IsDefault = true; footer.Children.Add(button); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        Loaded += (_, _) => { label.Focus(); label.SelectAll(); }; PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Commit(); e.Handled = true; } };
    }
}

internal sealed record ExportChoice(string Label, ChartExportFormat Format, string Extension);
internal sealed class ChartExportEditor : Window
{
    public ChartExportEditor(Func<ChartExportFormat, string, Task<bool>> export, DiagramKind kind = DiagramKind.Hierarchy)
    {
        Ui.ApplyWindowStyle(this); Title = "Export diagram"; Width = 560; Height = 385; MinWidth = 440; MinHeight = 320; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - 32);
        var format = new ComboBox { ItemTemplate = Ui.DisplayTemplate("Label") }; format.ItemsSource = kind == DiagramKind.Network ? new ExportChoice[] { new("PDF · paper preview + node / lead details", ChartExportFormat.Pdf, "pdf"), new("CSV · nodes and leads", ChartExportFormat.NetworkCsv, "csv"), new("XML · SUMAPP graph", ChartExportFormat.NetworkXml, "xml") } : new ExportChoice[] {
            new("PDF · paper preview + full node details",ChartExportFormat.Pdf,"pdf"),new("XML · Microsoft Project (MSPDI)",ChartExportFormat.ProjectXml,"xml"),new("XML · Primavera P6 (PMXML)",ChartExportFormat.PrimaveraXml,"xml"),
            new("CSV · complete hierarchy",ChartExportFormat.HierarchyCsv,"csv"),new("CSV · Microsoft Project mapping",ChartExportFormat.ProjectCsv,"csv"),new("CSV · P6 WBS / activity mapping material",ChartExportFormat.PrimaveraCsv,"csv")}; format.SelectedIndex = 0;
        var version = new ComboBox { ItemsSource = ChartExports.PrimaveraVersions, SelectedIndex = 0, IsEnabled = false };
        format.SelectionChanged += (_, _) => version.IsEnabled = (format.SelectedItem as ExportChoice)?.Format == ChartExportFormat.PrimaveraXml;
        var error = new TextBlock { Foreground = Ui.Error, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
        var panel = new StackPanel(); panel.Children.Add(Ui.Field("Export format", format)); panel.Children.Add(Ui.Field("P6 XML target version · use your version or an older one", version)); panel.Children.Add(new TextBlock { Text = kind == DiagramKind.Network ? "Connection exports preserve nodes, positions and directional leads. Free-form links are diagram relationships; use WBS mode for Microsoft Project / P6 planning exports." : "XML exports carry the hierarchy directly. CSV needs field mapping; P6 CSV rows must be copied into a P6-exported XLSX template. See PLANNING-EXPORTS.md included with the app.", TextWrapping = TextWrapping.Wrap, FontSize = 15, Margin = new Thickness(0, 12, 0, 0) }); panel.Children.Add(error);
        var root = new DockPanel { Margin = new Thickness(18) }; var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; var cancel = Ui.Button("Cancel", (_, _) => Close()); cancel.IsCancel = true; footer.Children.Add(cancel);
        var saving = false;
        var save = Ui.Button("Choose file / export", async (sender, _) =>
        {
            if (saving) return;
            var button = (Button)sender; saving = true; button.IsEnabled = false; cancel.IsEnabled = false;
            error.Text = "Preparing export…";
            try { if (await export(((ExportChoice)format.SelectedItem).Format, (string)version.SelectedItem)) DialogResult = true; else error.Text = ""; }
            catch (Exception ex) { error.Text = "Export not completed: " + ex.Message; }
            finally { saving = false; button.IsEnabled = true; cancel.IsEnabled = true; }
        }, true);
        Closing += (_, e) => { if (saving && DialogResult != true) e.Cancel = true; };
        save.Margin = new Thickness(8, 0, 0, 0); footer.Children.Add(save); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer); root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
    }
}
