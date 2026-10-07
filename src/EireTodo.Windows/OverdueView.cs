using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed class OverdueView : Border
{
    private readonly TodoService service;
    private readonly DataGrid table = new() { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, CanUserDeleteRows = false, MinHeight = 25, RowHeight = 40 };
    private readonly CheckBox history = new() { Content = "Include resolved history", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock count = new() { FontSize = 15, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
    public OverdueView(TodoService service, Action hide)
    {
        this.service = service; Background = (Brush)Application.Current.FindResource("PanelBrush"); BorderBrush = (Brush)Application.Current.FindResource("LineBrush"); BorderThickness = new Thickness(1); Padding = new Thickness(10); Margin = new Thickness(0, 8, 0, 0);
        var root = new Grid(); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); var header = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) }; header.Children.Add(count); header.Children.Add(history); RibbonBar.Action(header, "Hide log", hide); root.Children.Add(header); Grid.SetRow(table, 1); root.Children.Add(table); Child = root;
        foreach (var (title, property, width) in new[] { ("Module / diagram", "Location", 210d), ("Item", "Title", 300d), ("Due date", "Due", 140d), ("First seen", "Seen", 180d), ("State", "State", 160d), ("Resolved", "Resolved", 180d) })
            table.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(property), Width = width, ElementStyle = (Style)Application.Current.FindResource("GridText") });
        var style = new Style(typeof(DataGridRow), (Style)Application.Current.FindResource(typeof(DataGridRow)));
        var trigger = new DataTrigger { Binding = new Binding("Active"), Value = true }; trigger.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(BrandTheme.Error)))); style.Triggers.Add(trigger); table.RowStyle = style;
        var cells = new Style(typeof(DataGridCell), (Style)Application.Current.FindResource(typeof(DataGridCell))); var active = new DataTrigger { Binding = new Binding("Active"), Value = true }; active.Setters.Add(new Setter(Control.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(BrandTheme.Error)))); cells.Triggers.Add(active); table.CellStyle = cells;
        history.Checked += (_, _) => Refresh(); history.Unchecked += (_, _) => Refresh();
    }
    public void Refresh()
    {
        var entries = service.Data.OverdueLog; count.Text = $"OVERDUE LOG · {entries.Count(e => e.Resolution == OverdueResolution.Active)} active";
        table.ItemsSource = entries.Where(e => history.IsChecked == true || e.Resolution == OverdueResolution.Active).OrderByDescending(e => e.Resolution == OverdueResolution.Active).ThenBy(e => e.DueDate).Select(e => new OverdueRow(e)).ToList();
    }
}
internal sealed class OverdueRow(OverdueEntry entry)
{
    public string Location => (entry.Kind == OverdueItemKind.Task ? "To-do · " : "Chart · ") + entry.Location;
    public string Title => entry.Title;
    public string Due => AustralianDates.Format(entry.DueDate);
    public string Seen => AustralianDates.Format(entry.FirstSeen);
    public string Resolved => entry.ResolvedAt.HasValue ? AustralianDates.Format(entry.ResolvedAt.Value) : "";
    public bool Active => entry.Resolution == OverdueResolution.Active;
    public string State => Active ? "Overdue" : entry.Resolution.ToString();
}
