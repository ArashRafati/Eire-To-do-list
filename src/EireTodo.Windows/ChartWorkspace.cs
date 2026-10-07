using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed record LayoutChoice(string Label, ChartLayout Value);
internal sealed class ChartWorkspace : UserControl
{
    private readonly TodoService service;
    private readonly string dataDirectory;
    private readonly ComboBox diagrams = new() { Width = 230, ItemTemplate = Ui.DisplayTemplate("Name"), Margin = new Thickness(0, 0, 8, 6) };
    private readonly ComboBox layouts = new() { Width = 255, ItemTemplate = Ui.DisplayTemplate("Label"), Margin = new Thickness(0, 0, 8, 6) };
    private readonly Canvas canvas = new() { Background = new SolidColorBrush(Color.FromRgb(8, 12, 18)) };
    private readonly ScrollViewer viewport = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = true };
    private readonly ScrollViewer controls = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 195 };
    private readonly TextBlock status = new() { FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBlock empty = new() { Text = "Create a diagram to start. Enter adds a sibling; Insert adds a child.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Slider zoom = new() { Minimum = .2, Maximum = 2, Value = 1, Width = 95, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 6) };
    private readonly TextBlock zoomLabel = new() { Text = "100%", FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, FontSize = 15, Margin = new Thickness(0, 0, 8, 6) };
    private readonly Stack<Diagram> undo = new(), redo = new();
    private readonly List<Button> nodeActions = [];
    private Button undoButton = null!, redoButton = null!, balanceButton = null!;
    private Guid? chartId, selectedId;
    private ChartLayout defaultLayout = ChartLayout.MindMap;
    private bool refreshing;
    private ChartScene scene = new([], 500, 300);
    private Dictionary<Guid, Border> nodeViews = [];
    private Guid? renderedChartId;
    private int renderVersion;
    private Diagram? Current => service.Data.Diagrams.FirstOrDefault(d => d.Id == chartId);
    private Window OwnerWindow => Window.GetWindow(this);

    public ChartWorkspace(TodoService service, string dataDirectory)
    {
        this.service = service; this.dataDirectory = dataDirectory;
        var root = new Grid(); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var toolbars = new StackPanel();
        var first = new WrapPanel(); first.Children.Add(diagrams); Add(first, "+ Diagram", NewDiagram, true); Add(first, "Settings / rename", RenameDiagram); Add(first, "Delete diagram", DeleteDiagram); Add(first, "From to-do…", FromTodo);
        var second = new WrapPanel(); second.Children.Add(layouts); Add(second, "+ Sibling · Enter", () => AddNode(false), true); Add(second, "+ Child · Insert", () => AddNode(true), true); NodeAction(second, "Edit / notes · F2", EditNode); NodeAction(second, "Delete node", DeleteNode);
        var third = new WrapPanel(); NodeAction(third, "↑ Move up", () => Modify(c => Charts.Reorder(c, selectedId!.Value, -1))); NodeAction(third, "↓ Move down", () => Modify(c => Charts.Reorder(c, selectedId!.Value, 1))); NodeAction(third, "Indent", () => Modify(c => Charts.Indent(c, selectedId!.Value))); NodeAction(third, "Outdent", () => Modify(c => Charts.Outdent(c, selectedId!.Value))); NodeAction(third, "Fold / unfold", ToggleFold);
        undoButton = Add(third, "Undo", Undo); redoButton = Add(third, "Redo", Redo); Add(third, "Unfold all", () => Modify(c => c.Nodes.ForEach(n => n.Collapsed = false)));
        var fourth = new WrapPanel(); Add(fourth, "Export CSV / XML / PDF", Export, true); fourth.Children.Add(new TextBlock { Text = "Zoom", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 6), FontSize = 15 }); fourth.Children.Add(zoom); fourth.Children.Add(zoomLabel); Add(fourth, "Fit chart", Fit); balanceButton = Add(fourth, "Balance branches", () => Modify(MindMapPlacement.Rebalance));
        balanceButton.ToolTip = "Redistribute two-sided mind-map branches by subtree size. Existing sides otherwise stay fixed.";
        toolbars.Children.Add(first); toolbars.Children.Add(second); toolbars.Children.Add(third); toolbars.Children.Add(fourth);
        var card = new Border { Background = (Brush)Application.Current.FindResource("PanelBrush"), BorderBrush = new SolidColorBrush(Color.FromRgb(51, 70, 93)), BorderThickness = new Thickness(1), Padding = new Thickness(10), Child = toolbars, Margin = new Thickness(0, 0, 0, 10) };
        controls.Content = card; root.Children.Add(controls);
        var stage = new Grid(); stage.Children.Add(viewport); stage.Children.Add(empty); Grid.SetRow(stage, 1); root.Children.Add(stage); viewport.Content = canvas; Grid.SetRow(status, 2); root.Children.Add(status); Content = root;
        layouts.ItemsSource = Enum.GetValues<ChartLayout>().Select(l => new LayoutChoice(Charts.LayoutName(l), l)).ToList();
        diagrams.SelectionChanged += DiagramChanged; layouts.SelectionChanged += LayoutChanged; zoom.ValueChanged += ZoomChanged;
        viewport.PreviewKeyDown += KeyDownChart; viewport.PreviewMouseWheel += ZoomWheel;
        canvas.MouseLeftButtonDown += (_, e) => { if (e.Source == canvas) { viewport.Focus(); e.Handled = true; } };
        SizeChanged += (_, _) => controls.MaxHeight = Math.Max(68, ActualHeight - 160);
        RefreshData();
    }
    private Button Add(Panel panel, string text, Action action, bool primary = false)
    {
        var button = Ui.Button(text, (_, _) => action(), primary); button.Margin = new Thickness(0, 0, 6, 6); button.FontSize = 15; button.MinHeight = 38; panel.Children.Add(button); return button;
    }
    private void NodeAction(Panel panel, string text, Action action) => nodeActions.Add(Add(panel, text, () => { if (selectedId.HasValue) action(); }));
    public void SetMode(AppMode mode, bool changeLayout = true)
    {
        defaultLayout = mode == AppMode.Wbs ? ChartLayout.TopDown : ChartLayout.MindMap;
        var c = Current;
        if (changeLayout && c is not null && c.Layout != ChartLayout.Outline &&
            (mode == AppMode.Wbs && c.Layout is ChartLayout.MindMap or ChartLayout.RightTree || mode == AppMode.MindMap && c.Layout is ChartLayout.TopDown or ChartLayout.LeftToRight))
            Modify(d => d.Layout = defaultLayout, false);
        else Render();
    }
    public void RefreshData(bool resetHistory = false)
    {
        refreshing = true;
        var items = service.Data.Diagrams.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var requested = resetHistory ? service.Data.Settings.SelectedDiagramId : chartId ?? service.Data.Settings.SelectedDiagramId;
        var selected = items.FirstOrDefault(d => d.Id == requested) ?? items.FirstOrDefault();
        diagrams.ItemsSource = items; diagrams.SelectedItem = selected; chartId = selected?.Id;
        if (resetHistory) { undo.Clear(); redo.Clear(); selectedId = null; }
        if (Current is Diagram chart)
        {
            layouts.SelectedItem = ((IEnumerable<LayoutChoice>)layouts.ItemsSource).First(l => l.Value == chart.Layout); zoom.Value = chart.Zoom;
            if (!chart.Nodes.Any(n => n.Id == selectedId)) selectedId = chart.Nodes.FirstOrDefault()?.Id;
        }
        refreshing = false; Render();
    }
    private void DiagramChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing) return; var old = chartId; var next = (diagrams.SelectedItem as Diagram)?.Id;
        if (next == old) return;
        if (!Try(() => service.Change(d => d.Settings.SelectedDiagramId = next))) { RefreshData(); return; }
        chartId = next; selectedId = null; undo.Clear(); redo.Clear(); RefreshData();
    }
    private void LayoutChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!refreshing && layouts.SelectedItem is LayoutChoice choice && Current?.Layout != choice.Value) Modify(d => d.Layout = choice.Value, false);
    }
    private void NewDiagram()
    {
        var candidate = Charts.Create("New diagram", defaultLayout);
        var editor = new DiagramEditor(service, candidate, SaveNew) { Owner = OwnerWindow };
        if (editor.ShowDialog() == true) { RefreshData(); viewport.Focus(); }
    }
    private void SaveNew(Diagram candidate)
    {
        Charts.Validate(candidate);
        service.Change(d => { d.Diagrams.Add(candidate.Clone()); d.Settings.SelectedDiagramId = candidate.Id; });
        chartId = candidate.Id; selectedId = candidate.Nodes.FirstOrDefault()?.Id; undo.Clear(); redo.Clear();
    }
    private void RenameDiagram()
    {
        if (Current is not Diagram chart) return;
        if (new DiagramEditor(service, chart.Clone(), Commit) { Owner = OwnerWindow }.ShowDialog() == true) { RefreshData(); viewport.Focus(); }
    }
    private void DeleteDiagram()
    {
        if (Current is not Diagram chart) return;
        if (MessageBox.Show(OwnerWindow, $"Delete diagram '{chart.Name}' and all {chart.Nodes.Count} nodes?", "Confirm diagram deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (Try(() => service.DeleteDiagram(chart.Id))) { chartId = null; RefreshData(true); }
    }
    private void FromTodo()
    {
        // Choose a project explicitly; to-do filters never silently omit chart material.
        var picker = new Window { Title = "Create diagram from to-do", Width = 500, Height = 260, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = OwnerWindow }; Ui.ApplyWindowStyle(picker);
        var box = new ComboBox { ItemTemplate = Ui.DisplayTemplate("DisplayName"), ItemsSource = service.Data.Projects.OrderBy(p => p.Name).ToList(), SelectedIndex = 0 };
        var completed = new CheckBox { Content = "Include completed tasks", IsChecked = true, Margin = new Thickness(0, 12, 0, 12) };
        var panel = new StackPanel { Margin = new Thickness(18) }; panel.Children.Add(Ui.Field("Project to copy · existing tasks stay available", box)); panel.Children.Add(completed);
        panel.Children.Add(Ui.Button("Create diagram", (_, _) =>
        {
            if (box.SelectedItem is not Project project) return;
            if (Try(() =>
            {
                var tasks = service.Data.Tasks.Where(t => t.ProjectId == project.Id && (completed.IsChecked == true || !t.Completed)).ToList();
                if (tasks.Count > Charts.MaxNodes - 1) throw new ArgumentException("This project exceeds the 999-task diagram limit.");
                if (tasks.Any(t => t.Description.Trim().Length > 100)) throw new ArgumentException("Some task descriptions exceed the 100-character planning node limit. Shorten them before copying this project.");
                var chart = Charts.Create(project.Name, defaultLayout, project.Id); var root = chart.Nodes[0].Id;
                foreach (var task in tasks) { var node = Charts.Add(chart, root, true, task.Description); node.Notes = task.Notes; node.StartDate = task.StartDate; node.FinishDate = task.FinishDate; node.Completed = task.Completed; }
                SaveNew(chart); RefreshData(); picker.DialogResult = true;
            })) viewport.Focus();
        }, true)); picker.Content = panel; picker.ShowDialog();
    }
    private void Commit(Diagram candidate)
    {
        var previous = Current?.Clone(); service.SaveDiagram(candidate);
        if (previous is not null) { undo.Push(previous); redo.Clear(); LimitHistory(); }
    }
    private void LimitHistory()
    {
        // Restrict undo memory even when notes are large. Oldest snapshots are discarded.
        var kept = new List<Diagram>(); long bytes = 0;
        foreach (var item in undo.Take(25))
        {
            var size = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(item, DataDocument.JsonOptions).Length;
            if (kept.Count > 0 && bytes + size > 16 * 1024 * 1024) break;
            kept.Add(item); bytes += size;
        }
        undo.Clear(); foreach (var item in kept.AsEnumerable().Reverse()) undo.Push(item);
    }
    private void Modify(Action<Diagram> change, bool focus = true)
    {
        if (Current is not Diagram chart) return;
        if (Try(() => { var candidate = chart.Clone(); change(candidate); Commit(candidate); })) { RefreshData(); if (focus) viewport.Focus(); }
        else RefreshData();
    }
    private void AddNode(bool child)
    {
        if (Current is not Diagram chart) { NewDiagram(); return; }
        if (child && !selectedId.HasValue) { MessageBox.Show(OwnerWindow, "Select a parent node first.", "Add child"); return; }
        Guid? added = null;
        if (!Try(() => { var candidate = chart.Clone(); added = Charts.Add(candidate, selectedId, child).Id; Commit(candidate); })) return;
        selectedId = added; RefreshData(); viewport.Focus(); ScrollToSelection(); EditNode();
    }
    private void EditNode()
    {
        if (Current is not Diagram chart || selectedId is not Guid id) return;
        if (new NodeEditor(chart.Clone(), id, Commit) { Owner = OwnerWindow }.ShowDialog() == true) RefreshData();
        viewport.Focus(); ScrollToSelection();
    }
    private void DeleteNode()
    {
        if (Current is not Diagram chart || selectedId is not Guid id) return;
        var count = Charts.Descendants(chart, id).Count; var parent = chart.Nodes.Single(n => n.Id == id).ParentId;
        if (MessageBox.Show(OwnerWindow, $"Delete this node and its branch ({count} nodes)?", "Confirm node deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        Modify(c => Charts.Delete(c, id)); if (Current?.Nodes.Any(n => n.Id == id) != true) { selectedId = parent; RefreshData(); }
    }
    private void ToggleFold() => Modify(c => { var n = c.Nodes.Single(n => n.Id == selectedId); n.Collapsed = !n.Collapsed; });
    private void Undo()
    {
        if (Current is not Diagram chart || !undo.TryPeek(out var previous)) return;
        if (Try(() => service.SaveDiagram(previous))) { undo.Pop(); redo.Push(chart.Clone()); RefreshData(); viewport.Focus(); }
    }
    private void Redo()
    {
        if (Current is not Diagram chart || !redo.TryPeek(out var next)) return;
        if (Try(() => service.SaveDiagram(next))) { redo.Pop(); undo.Push(chart.Clone()); RefreshData(); viewport.Focus(); }
    }
    private void Export()
    {
        if (Current is not Diagram chart) { MessageBox.Show(OwnerWindow, "Create or select a diagram first.", "Export diagram"); return; }
        new ChartExportEditor(async (format, version) =>
        {
            var ext = format == ChartExportFormat.Pdf ? "pdf" : format is ChartExportFormat.ProjectXml or ChartExportFormat.PrimaveraXml ? "xml" : "csv";
            var safeName = string.Concat(chart.Name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var file = new SaveFileDialog { Title = "Export complete diagram", Filter = $"{ext.ToUpperInvariant()} files (*.{ext})|*.{ext}", DefaultExt = "." + ext, AddExtension = true, FileName = safeName + "-" + format + "." + ext };
            if (file.ShowDialog(OwnerWindow) != true) return false;
            var path = System.IO.Path.GetFullPath(file.FileName);
            if (path.StartsWith(System.IO.Path.GetFullPath(dataDirectory) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose an export folder outside the app's data folder.");
            await System.Threading.Tasks.Task.Run(() => ChartExports.Save(chart, format, path, version));
            MessageBox.Show(OwnerWindow, "Diagram exported. All nodes are included, even inside folded branches.\n\n" + path, "Export complete", MessageBoxButton.OK, MessageBoxImage.Information); return true;
        })
        { Owner = OwnerWindow }.ShowDialog();
    }
    private bool Try(Action action)
    {
        try { action(); return true; } catch (Exception ex) { MessageBox.Show(OwnerWindow, "The change or export could not be saved. Previous saved data remains available.\n\n" + ex.Message, "Chart operation failed", MessageBoxButton.OK, MessageBoxImage.Error); return false; }
    }
    private void Render()
    {
        var oldScene = scene;
        var oldOffset = new Point(viewport.HorizontalOffset, viewport.VerticalOffset);
        var oldChartId = renderedChartId;
        var version = ++renderVersion;
        canvas.Children.Clear(); nodeViews.Clear();
        var chart = Current; renderedChartId = chart?.Id; balanceButton.IsEnabled = chart?.Layout == ChartLayout.MindMap; empty.Visibility = chart is null || chart.Nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in nodeActions) button.IsEnabled = selectedId.HasValue && chart?.Nodes.Any(n => n.Id == selectedId) == true;
        undoButton.IsEnabled = undo.Count > 0; redoButton.IsEnabled = redo.Count > 0;
        if (chart is null) { canvas.Width = 500; canvas.Height = 300; status.Text = "Offline · diagrams save with your tasks and backups."; return; }
        scene = ChartGeometry.Arrange(chart); var index = scene.Boxes.ToDictionary(b => b.Id); var outlines = Charts.Outline(chart, true);
        var nodeIndex = outlines.ToDictionary(o => o.Node.Id);
        if (selectedId.HasValue && !nodeIndex.ContainsKey(selectedId.Value))
        {
            var hidden = chart.Nodes.FirstOrDefault(n => n.Id == selectedId);
            while (hidden?.ParentId is Guid parent && !nodeIndex.ContainsKey(hidden.Id)) hidden = chart.Nodes.First(n => n.Id == parent);
            selectedId = hidden is not null && nodeIndex.ContainsKey(hidden.Id) ? hidden.Id : outlines.FirstOrDefault()?.Node.Id;
        }
        foreach (var box in scene.Boxes)
        {
            var node = nodeIndex[box.Id].Node;
            if (node.ParentId is not Guid parent || !index.TryGetValue(parent, out var from)) continue;
            var c = ChartGeometry.Connector(from, box, chart.Layout);
            var path = new System.Windows.Shapes.Path { Stroke = new SolidColorBrush(Color.FromRgb(105, 133, 161)), StrokeThickness = 1.6, IsHitTestVisible = false };
            var geometry = new StreamGeometry(); using (var context = geometry.Open())
            {
                context.BeginFigure(new Point(c.X1, c.Y1), false, false);
                if (chart.Layout is ChartLayout.MindMap or ChartLayout.RightTree) context.BezierTo(new Point((c.X1 + c.X2) / 2, c.Y1), new Point((c.X1 + c.X2) / 2, c.Y2), new Point(c.X2, c.Y2), true, false);
                else if (chart.Layout == ChartLayout.TopDown) { context.LineTo(new Point(c.X1, (c.Y1 + c.Y2) / 2), true, false); context.LineTo(new Point(c.X2, (c.Y1 + c.Y2) / 2), true, false); context.LineTo(new Point(c.X2, c.Y2), true, false); }
                else { context.LineTo(new Point((c.X1 + c.X2) / 2, c.Y1), true, false); context.LineTo(new Point((c.X1 + c.X2) / 2, c.Y2), true, false); context.LineTo(new Point(c.X2, c.Y2), true, false); }
            }
            geometry.Freeze(); path.Data = geometry; canvas.Children.Add(path);
        }
        foreach (var box in scene.Boxes)
        {
            var o = nodeIndex[box.Id]; var n = o.Node; var content = new Grid { Margin = new Thickness(11, 8, 9, 7) }; content.RowDefinitions.Add(new() { Height = GridLength.Auto }); content.RowDefinitions.Add(new()); content.RowDefinitions.Add(new() { Height = GridLength.Auto });
            content.Children.Add(new TextBlock { Text = o.Code + "   " + (n.Collapsed && o.Summary ? "+ " : "") + (n.Completed ? "COMPLETED" : o.Summary ? "BRANCH" : n.ActivityId), FontFamily = new FontFamily("Consolas"), FontSize = 14, Foreground = Ui.Accent, TextTrimming = TextTrimming.CharacterEllipsis });
            var label = new TextBlock { Text = n.Title, FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 46, Margin = new Thickness(0, 4, 0, 2) }; Grid.SetRow(label, 1); content.Children.Add(label);
            var preview = string.Join(" ", n.Notes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)); var meta = preview.Length > 0 ? preview : $"{AustralianDates.Format(n.StartDate)} → {AustralianDates.Format(n.FinishDate)}";
            var note = new TextBlock { Text = meta, FontSize = 15, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis }; Grid.SetRow(note, 2); content.Children.Add(note);
            var border = new Border { Width = box.Width, Height = box.Height, Background = new SolidColorBrush(Color.FromRgb(16, 23, 33)), BorderBrush = new SolidColorBrush(Color.FromRgb(66, 84, 107)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(chart.Layout is ChartLayout.MindMap or ChartLayout.RightTree ? 8 : 2), Child = content, Tag = n.Id, ToolTip = n.Title + "\nDouble-click or F2 to edit / open full notes.", Focusable = true };
            border.MouseLeftButtonDown += (_, e) => { Select(n.Id); viewport.Focus(); if (e.ClickCount == 2) EditNode(); e.Handled = true; };
            Canvas.SetLeft(border, box.X); Canvas.SetTop(border, box.Y); canvas.Children.Add(border); nodeViews[n.Id] = border;
        }
        canvas.Width = scene.Width; canvas.Height = scene.Height; canvas.LayoutTransform = new ScaleTransform(chart.Zoom, chart.Zoom); zoomLabel.Text = $"{chart.Zoom:P0}"; Select(selectedId);
        status.Text = $"{chart.Name} · {chart.Nodes.Count} nodes · {outlines.Count} visible · Enter: sibling · Insert: child · F2: edit · Ctrl+Z/Y: undo/redo";
        // Retain the selected node/nearest existing ancestor in the viewport as the chart reflows.
        if (oldChartId == chart.Id)
        {
            var anchor = chart.Nodes.FirstOrDefault(n => n.Id == selectedId);
            while (anchor is not null && !oldScene.Boxes.Any(b => b.Id == anchor.Id))
                anchor = anchor.ParentId.HasValue ? chart.Nodes.First(n => n.Id == anchor.ParentId) : null;
            var before = oldScene.Boxes.FirstOrDefault(b => b.Id == anchor?.Id);
            var after = scene.Boxes.FirstOrDefault(b => b.Id == anchor?.Id);
            if (before is not null && after is not null)
                Dispatcher.InvokeAsync(() =>
                {
                    if (version != renderVersion) return;
                    viewport.ScrollToHorizontalOffset(Math.Max(0, oldOffset.X + (after.X - before.X) * chart.Zoom));
                    viewport.ScrollToVerticalOffset(Math.Max(0, oldOffset.Y + (after.Y - before.Y) * chart.Zoom));
                }, DispatcherPriority.Loaded);
        }

    }
    private void Select(Guid? id)
    {
        selectedId = id;
        foreach (var (node, view) in nodeViews) { view.BorderBrush = node == id ? Ui.Accent : new SolidColorBrush(Color.FromRgb(66, 84, 107)); view.BorderThickness = new Thickness(node == id ? 2 : 1); view.Background = new SolidColorBrush(node == id ? Color.FromRgb(28, 42, 57) : Color.FromRgb(16, 23, 33)); }
        foreach (var button in nodeActions) button.IsEnabled = id.HasValue && Current?.Nodes.Any(n => n.Id == id) == true;
    }
    private void ScrollToSelection()
    {
        var id = selectedId;
        // New cells need a completed WPF measure/arrange pass before BringIntoView has real bounds.
        Dispatcher.InvokeAsync(() =>
        {
            if (id == selectedId && id is Guid selected && nodeViews.TryGetValue(selected, out var view)) view.BringIntoView();
        }, DispatcherPriority.Loaded);
    }
    private void KeyDownChart(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.Z) { Undo(); e.Handled = true; }
            else if (e.Key == Key.Y) { Redo(); e.Handled = true; }
            else if (e.Key == Key.Up && selectedId.HasValue) { Modify(c => Charts.Reorder(c, selectedId.Value, -1)); e.Handled = true; }
            else if (e.Key == Key.Down && selectedId.HasValue) { Modify(c => Charts.Reorder(c, selectedId.Value, 1)); e.Handled = true; }
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        switch (e.Key)
        {
            case Key.Enter: AddNode(false); e.Handled = true; break;
            case Key.Insert: AddNode(true); e.Handled = true; break;
            case Key.F2: EditNode(); e.Handled = true; break;
            case Key.Delete: DeleteNode(); e.Handled = true; break;
            case Key.Up:
            case Key.Down:
                if (Current is Diagram chart) { var visible = Charts.Outline(chart, true); var index = visible.FindIndex(o => o.Node.Id == selectedId); if (visible.Count > 0) { Select(visible[Math.Clamp(index + (e.Key == Key.Up ? -1 : 1), 0, visible.Count - 1)].Node.Id); ScrollToSelection(); } }
                e.Handled = true; break;
            case Key.Left: if (selectedId.HasValue) ToggleFold(); e.Handled = true; break;
        }
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (refreshing || Current is not Diagram chart) return;
        var value = Math.Round(zoom.Value, 2); if (Math.Abs(chart.Zoom - value) < .005) return;
        // Zoom is a saved view preference, outside the node undo history.
        if (Try(() => { var candidate = chart.Clone(); candidate.Zoom = value; service.SaveDiagram(candidate); })) { canvas.LayoutTransform = new ScaleTransform(value, value); zoomLabel.Text = $"{value:P0}"; }
        else { refreshing = true; zoom.Value = chart.Zoom; refreshing = false; }
    }
    private void ZoomWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return; zoom.Value = Math.Clamp(zoom.Value + (e.Delta > 0 ? .1 : -.1), .2, 2); e.Handled = true;
    }
    private void Fit()
    {
        if (Current is null) return; zoom.Value = Math.Clamp(Math.Min((viewport.ViewportWidth - 20) / scene.Width, (viewport.ViewportHeight - 20) / scene.Height), .2, 2); viewport.ScrollToTop(); viewport.ScrollToLeftEnd(); viewport.Focus();
    }
}
