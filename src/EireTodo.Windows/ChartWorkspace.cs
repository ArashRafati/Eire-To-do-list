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

internal sealed partial class ChartWorkspace : UserControl
{
    private readonly TodoService service;
    private readonly string dataDirectory;
    private readonly ComboBox diagrams = new() { Width = 210, MinHeight = 34, FontSize = 15, ItemTemplate = Ui.DisplayTemplate("Name"), Margin = new Thickness(0, 0, 8, 6) };
    private DiagramGallery paletteGallery = null!, designGallery = null!, layoutGallery = null!;
    private readonly Canvas canvas = new() { Background = Ui.Brush("Surface") };
    private readonly ScrollViewer viewport = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = true };
    private readonly ScrollViewer controls = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = 195 };
    private readonly TextBlock status = new() { FontSize = 15, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) };
    private readonly TextBlock empty = new() { Text = "Create a diagram to start. Enter adds a sibling; Insert adds a child.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private readonly Slider zoom = new() { Minimum = .2, Maximum = 2, Value = 1, Width = 95, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 6) };
    private readonly TextBlock zoomLabel = new() { Text = "100%", FontFamily = new FontFamily("Consolas"), VerticalAlignment = VerticalAlignment.Center, FontSize = 15, Margin = new Thickness(0, 0, 8, 6) };
    private readonly Stack<Diagram> undo = new(), redo = new();
    private readonly List<Button> nodeActions = [];
    private Button undoButton = null!, redoButton = null!, balanceButton = null!;
    private Guid? chartId, selectedId;
    private ChartLayout defaultLayout = ChartLayout.MindMap;
    private bool refreshing;
    private RibbonBar ribbon = null!;
    private ChartScene scene = new([], 500, 300);
    private Dictionary<Guid, Border> nodeViews = [];
    private Guid? renderedChartId;
    private int renderVersion;
    private Diagram? Current => service.Data.Diagrams.FirstOrDefault(d => d.Id == chartId);
    private Window OwnerWindow => Window.GetWindow(this);

    public ChartWorkspace(TodoService service, string dataDirectory, FormatPainter painter, Action showOverdue, Action changed, RibbonBar sharedRibbon)
    {
        this.service = service; this.dataDirectory = dataDirectory; this.painter = painter; this.changed = changed;
        var root = new Grid(); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        ribbon = sharedRibbon;
        var first = ribbon.Group("Home", "Diagram"); first.Children.Add(diagrams); Add(first, "New diagram", NewDiagram, true); Add(first, "Settings / rename", RenameDiagram); Add(first, "Delete diagram", DeleteDiagram);
        var edit = ribbon.Group("Home", "Edit"); NodeAction(edit, "Edit title", BeginTitle); NodeAction(edit, "Node details", EditNode); NodeAction(edit, "Delete node", DeleteNode);
        var history = ribbon.Group("Home", "History"); undoButton = Add(history, "Undo", Undo); redoButton = Add(history, "Redo", Redo);
        var second = ribbon.Group("Insert", "Nodes"); siblingButton = Add(second, "Sibling", () => AddNode(false), true); siblingButton.ToolTip = "Enter: create a sibling, or a linked graph node"; childButton = Add(second, "Child", () => AddNode(true), true); childButton.ToolTip = "Insert: create a child, or a linked graph node";
        unlinkedControls = ribbon.Group("Insert", "Independent node"); Add(unlinkedControls, "Unlinked node", () => AddNode(false, true));
        var leads = ribbon.Group("Insert", "Leads"); leadControls = leads; Add(leads, "Connect nodes", StartConnection, true); Add(leads, "Cancel lead", () => { connectFrom = null; status.Text = "Lead cancelled."; }); Add(leads, "Lead label", EditSelectedLead); Add(leads, "Delete lead", DeleteSelectedLead);
        var choices = ribbon.Group("Insert", "New lead style"); leadOptions = choices; choices.Children.Add(leadDirection); choices.Children.Add(leadRouting);
        var copies = ribbon.Group("Insert", "Import tasks"); fromTodoButton = Add(copies, "From to-do…", FromTodo);
        var third = ribbon.Group("View", "Hierarchy"); hierarchyControls = third; NodeAction(third, "Earlier", () => Modify(c => Charts.Reorder(c, selectedId!.Value, -1))); NodeAction(third, "Later", () => Modify(c => Charts.Reorder(c, selectedId!.Value, 1))); NodeAction(third, "Indent", () => Modify(c => Charts.Indent(c, selectedId!.Value))); NodeAction(third, "Outdent", () => Modify(c => Charts.Outdent(c, selectedId!.Value))); NodeAction(third, "Fold / unfold", ToggleFold); Add(third, "Unfold all", () => Modify(c => c.Nodes.ForEach(n => n.Collapsed = false)));
        var formatting = ribbon.Group("Format", "Text"); formatTools = new FormattingTools(SelectedFormat, ApplyFormat, painter); formatting.Children.Add(formatTools);
        var layoutGroup = ribbon.Group("View", "Diagram layout"); layoutControls = layoutGroup;
        layoutGallery = new(DiagramGalleryKind.Layouts, index => { if (Current?.Layout != (ChartLayout)index) Modify(c => { c.Layout = (ChartLayout)index; FreePlacement.Reset(c); }, false); }); layoutGroup.Children.Add(layoutGallery);
        var placement = ribbon.Group("View", "Placement"); placementControls = placement; placement.Children.Add(freeMoveToggle); freeMoveToggle.Checked += (_, _) => SetFreeMove(true); freeMoveToggle.Unchecked += (_, _) => SetFreeMove(false); Add(placement,"Reset positions",() => Modify(FreePlacement.Reset)); balanceButton = Add(placement, "Balance branches", () => Modify(MindMapPlacement.Rebalance)); balanceButton.ToolTip = "Redistribute mind-map branches by subtree size.";
        var appearance = ribbon.Group("Format", "Node designs"); designGallery = new(DiagramGalleryKind.Styles, index => { if (Current?.Design != (NodeDesign)index) Modify(c => c.Design = (NodeDesign)index, false); }); appearance.Children.Add(designGallery);
        var colours = ribbon.Group("Format", "Colour combinations"); paletteGallery = new(DiagramGalleryKind.Colours, index => { if (Current?.Palette != (DiagramPalette)index) Modify(c => c.Palette = (DiagramPalette)index, false); }); colours.Children.Add(paletteGallery);
        var camera = ribbon.Group("View", "Canvas"); var scale = new StackPanel { Orientation = Orientation.Horizontal, Height = 34 }; scale.Children.Add(zoom); scale.Children.Add(zoomLabel); camera.Children.Add(scale); Add(camera, "Fit / centre", Fit);
        var output = ribbon.Group("Home", "Output"); Add(output, "Export", Export, true); Add(output, "Overdue log", showOverdue);
        var stage = new Grid(); stage.Children.Add(viewport); stage.Children.Add(empty); Grid.SetRow(stage, 0); root.Children.Add(stage); InitialiseSmoothZoom(); Grid.SetRow(status, 1); root.Children.Add(status); Content = root;
        diagrams.SelectionChanged += DiagramChanged; zoom.ValueChanged += ZoomChanged;
        viewport.PreviewKeyDown += KeyDownChart; viewport.PreviewMouseWheel += ZoomWheel;
        canvas.MouseLeftButtonDown += (_, e) => { if (e.Source == canvas) { viewport.Focus(); e.Handled = true; } };
        InitialiseOpenCanvas();
        Unloaded += (_, _) => EndFreeDrag(true);
        RefreshData();
    }
    private Button Add(Panel panel, string text, Action action, bool primary = false)
    {
        return RibbonBar.Action(panel, text, action, primary);
    }
    private void NodeAction(Panel panel, string text, Action action) => nodeActions.Add(Add(panel, text, () => { if (selectedId.HasValue) action(); }));
    public void SetMode(AppMode mode, bool changeLayout = true)
    {
        if (!FinishInlineEdit()) return;
        var network = mode == AppMode.Graph;
        if (network != networkMode) { networkMode = network; chartId = null; selectedId = null; selectedLeadId = null; undo.Clear(); redo.Clear(); connectFrom = null; }
        defaultLayout = network ? ChartLayout.Freeform : ChartLayout.MindMap;
        RibbonBar.ShowGroup(hierarchyControls, !network); RibbonBar.ShowGroup(leadControls, network); RibbonBar.ShowGroup(leadOptions, network); RibbonBar.ShowGroup(unlinkedControls, network); RibbonBar.ShowGroup(layoutControls, !network); RibbonBar.ShowGroup((Panel)fromTodoButton.Parent, !network);
        RibbonBar.ShowGroup(placementControls, !network); fromTodoButton.Visibility = network ? Visibility.Collapsed : Visibility.Visible;
        empty.Text = network ? "Create a graph to start. Enter / Insert adds a node linked from the selection." : "Create a diagram. Choose a mind-map or WBS layout in View.";
        RibbonBar.Label(siblingButton, network ? "Linked node" : "Sibling"); RibbonBar.Label(childButton, "Child"); childButton.Visibility = network ? Visibility.Collapsed : Visibility.Visible;
        RefreshData(); if (!network && Current is Diagram chart) defaultLayout = chart.Layout;
    }
    public void RefreshIfIdle() { if (inline is null && freeDragOriginal is null) RefreshData(); }
    internal void CaptureRibbon(string tab) => ribbon.Show(tab);
    public void RefreshData(bool resetHistory = false)
    {
        refreshing = true;
        var items = service.Data.Diagrams.Where(d => (d.Kind == DiagramKind.Network) == networkMode).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        var saved = networkMode ? service.Data.Settings.SelectedNetworkId : service.Data.Settings.SelectedDiagramId;
        var requested = resetHistory ? saved : chartId ?? saved;
        var selected = items.FirstOrDefault(d => d.Id == requested) ?? items.FirstOrDefault();
        diagrams.ItemsSource = items; diagrams.SelectedItem = selected; chartId = selected?.Id;
        if (resetHistory) { undo.Clear(); redo.Clear(); selectedId = null; }
        if (Current is Diagram chart)
        {
            zoom.Value = chart.Zoom;
            if (!chart.Leads.Any(l => l.Id == selectedLeadId) && !chart.Nodes.Any(n => n.Id == selectedId)) selectedId = chart.Nodes.FirstOrDefault()?.Id;
        }
        designGallery.Refresh(Current); paletteGallery.Refresh(Current); layoutGallery.Refresh(Current);
        freeMoveToggle.IsChecked = Current?.FreeMove == true; freeMoveToggle.IsEnabled = Current is not null;
        refreshing = false; Render();
    }
    private void DiagramChanged(object sender, SelectionChangedEventArgs e)
    {
        if (refreshing) return; if (!FinishInlineEdit()) { refreshing = true; diagrams.SelectedItem = diagrams.Items.Cast<Diagram>().FirstOrDefault(d => d.Id == chartId); refreshing = false; return; } var old = chartId; var next = (diagrams.SelectedItem as Diagram)?.Id;
        if (next == old) return;
        if (!Try(() => service.Change(d => { if (networkMode) d.Settings.SelectedNetworkId = next; else d.Settings.SelectedDiagramId = next; }))) { RefreshData(); return; }
        chartId = next; selectedId = null; undo.Clear(); redo.Clear(); RefreshData();
    }
    private void NewDiagram()
    {
        if (!FinishInlineEdit()) return;
        var candidate = networkMode ? NetworkCharts.Create("New diagram") : Charts.Create("New diagram", defaultLayout);
        var editor = new DiagramEditor(service, candidate, SaveNew) { Owner = OwnerWindow };
        if (editor.ShowDialog() == true) { RefreshData(); viewport.Focus(); }
    }
    private void SaveNew(Diagram candidate)
    {
        Charts.Validate(candidate);
        service.Change(d => { d.Diagrams.Add(candidate.Clone()); if (networkMode) d.Settings.SelectedNetworkId = candidate.Id; else d.Settings.SelectedDiagramId = candidate.Id; });
        changed(); chartId = candidate.Id; selectedId = candidate.Nodes.FirstOrDefault()?.Id; undo.Clear(); redo.Clear();
    }
    private void RenameDiagram()
    {
        if (!FinishInlineEdit()) return;
        if (Current is not Diagram chart) return;
        if (new DiagramEditor(service, chart.Clone(), Commit) { Owner = OwnerWindow }.ShowDialog() == true) { RefreshData(); viewport.Focus(); }
    }
    private void DeleteDiagram()
    {
        if (!FinishInlineEdit()) return;
        if (Current is not Diagram chart) return;
        if (MessageBox.Show(OwnerWindow, $"Delete diagram '{chart.Name}' and all {chart.Nodes.Count} nodes?", "Confirm diagram deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        if (Try(() => service.DeleteDiagram(chart.Id))) { chartId = null; changed(); RefreshData(true); }
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
                foreach (var task in tasks) { var node = Charts.Add(chart, root, true, task.Description); node.Notes = task.Notes; node.StartDate = task.StartDate; node.FinishDate = task.FinishDate; node.Completed = task.Completed; node.Priority = task.Priority; node.Format = task.Format.Clone(); }
                SaveNew(chart); RefreshData(); picker.DialogResult = true;
            })) viewport.Focus();
        }, true)); picker.Content = panel; picker.ShowDialog();
    }
    private void Commit(Diagram candidate)
    {
        var previous = Current?.Clone(); service.SaveDiagram(candidate); changed();
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
        if (!FinishInlineEdit() || Current is not Diagram chart) return;
        if (Try(() => { var candidate = chart.Clone(); change(candidate); Commit(candidate); })) { RefreshData(); if (focus) viewport.Focus(); }
        else RefreshData();
    }
    private void AddNode(bool child, bool unlinked = false)
    {
        if (!FinishInlineEdit()) return;
        if (Current is not Diagram chart) { NewDiagram(); return; }
        if (!networkMode && child && !selectedId.HasValue) { MessageBox.Show(OwnerWindow, "Select a parent node first.", "Add child"); return; }
        Guid? added = null;
        if (!Try(() => { var candidate = chart.Clone(); var n = networkMode ? GraphCreation.Add(candidate, selectedId, direction: Direction, routing: Routing, format: SelectedFormat(), unlinked: unlinked, measure: WindowsNodeMetrics.Measure) : Charts.Add(candidate, selectedId, child); if (selectedId is Guid source) n.Format = chart.Nodes.Single(x => x.Id == source).Format.Clone(); added = n.Id; Commit(candidate); })) return;
        selectedId = added; RefreshData(); viewport.Focus(); ScrollToSelection(); BeginTitle();
    }
    private void EditNode()
    {
        if (!FinishInlineEdit()) return;
        if (Current is not Diagram chart || selectedId is not Guid id) return;
        if (new NodeEditor(chart.Clone(), id, Commit) { Owner = OwnerWindow }.ShowDialog() == true) RefreshData();
        viewport.Focus(); ScrollToSelection();
    }
    private void DeleteNode()
    {
        if (!FinishInlineEdit()) return;
        if (Current is not Diagram chart || selectedId is not Guid id) return;
        var parent = chart.Nodes.Single(n => n.Id == id).ParentId;
        Modify(c => Charts.Delete(c, id)); if (Current?.Nodes.Any(n => n.Id == id) != true) { selectedId = parent; RefreshData(); }
    }
    private void ToggleFold() => Modify(c => { var n = c.Nodes.Single(n => n.Id == selectedId); n.Collapsed = !n.Collapsed; });
    private void Undo()
    {
        if (!FinishInlineEdit()) return;
        if (Current is not Diagram chart || !undo.TryPeek(out var previous)) return;
        if (Try(() => service.SaveDiagram(previous))) { undo.Pop(); redo.Push(chart.Clone()); changed(); RefreshData(); viewport.Focus(); }
    }
    private void Redo()
    {
        if (!FinishInlineEdit()) return;
        if (Current is not Diagram chart || !redo.TryPeek(out var next)) return;
        if (Try(() => service.SaveDiagram(next))) { redo.Pop(); undo.Push(chart.Clone()); changed(); RefreshData(); viewport.Focus(); }
    }
    private void Export()
    {
        if (!FinishInlineEdit()) return;
        if (Current is not Diagram chart) { MessageBox.Show(OwnerWindow, "Create or select a diagram first.", "Export diagram"); return; }
        new ChartExportEditor(async (format, version) =>
        {
            if (format == ChartExportFormat.Pdf)
            {
                var preview = new PdfPreviewWindow(chart,async bytes =>
                {
                    var file = new SaveFileDialog { Title = "Save previewed PDF", Filter = "PDF files (*.pdf)|*.pdf", DefaultExt = ".pdf", AddExtension = true, FileName = "Diagram.pdf" };
                    if (file.ShowDialog(OwnerWindow) != true) return false;
                    var path = System.IO.Path.GetFullPath(file.FileName);
                    if (path.StartsWith(System.IO.Path.GetFullPath(dataDirectory)+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose an export folder outside the app's data folder.");
                    await Task.Run(()=>DiagramPdf.SaveBytes(bytes,path)); return true;
                }) { Owner = OwnerWindow }; return preview.ShowDialog() == true;
            }
            var ext = format == ChartExportFormat.Pdf ? "pdf" : format is ChartExportFormat.ProjectXml or ChartExportFormat.PrimaveraXml or ChartExportFormat.NetworkXml ? "xml" : "csv";
            var safeName = string.Concat(chart.Name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var file = new SaveFileDialog { Title = "Export complete diagram", Filter = $"{ext.ToUpperInvariant()} files (*.{ext})|*.{ext}", DefaultExt = "." + ext, AddExtension = true, FileName = safeName + "-" + format + "." + ext };
            if (file.ShowDialog(OwnerWindow) != true) return false;
            var path = System.IO.Path.GetFullPath(file.FileName);
            if (path.StartsWith(System.IO.Path.GetFullPath(dataDirectory) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose an export folder outside the app's data folder.");
            await System.Threading.Tasks.Task.Run(() => ChartExports.Save(chart, format, path, version));
            MessageBox.Show(OwnerWindow, "Diagram exported. All nodes are included, even inside folded branches.\n\n" + path, "Export complete", MessageBoxButton.OK, MessageBoxImage.Information); return true;
        }, chart.Kind)
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
        canvas.Children.Clear(); nodeViews.Clear(); titleViews.Clear(); hierarchyPaths.Clear();
        var chart = Current; renderedChartId = chart?.Id; balanceButton.IsEnabled = chart?.Layout == ChartLayout.MindMap; empty.Visibility = chart is null || chart.Nodes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var button in nodeActions) button.IsEnabled = selectedId.HasValue && chart?.Nodes.Any(n => n.Id == selectedId) == true;
        undoButton.IsEnabled = undo.Count > 0; redoButton.IsEnabled = redo.Count > 0;
        if (chart is null) { canvas.Width = OpenWorkspace.Padding * 2; canvas.Height = OpenWorkspace.Padding * 2; status.Text = "Offline · diagrams save with your tasks and backups."; return; }
        var opening = oldChartId != chart.Id;
        PrepareOpenScene(chart, opening); var index = scene.Boxes.ToDictionary(b => b.Id); var outlines = Charts.Outline(chart, true);
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
            var path = new System.Windows.Shapes.Path { Stroke = Colour(DiagramAppearance.Colours(chart.Palette).Lead), StrokeThickness = 1.6, IsHitTestVisible = false, Data = HierarchyGeometry(from,box,chart.Layout,chart.FreeMove) };
            hierarchyPaths[box.Id] = path; canvas.Children.Add(path);
        }
        if (networkMode) DrawLeads(chart, index);
        var nodeLevels = DiagramAppearance.Levels(chart);
        foreach (var box in scene.Boxes)
        {
            var o = nodeIndex[box.Id]; var n = o.Node; var level = nodeLevels[n.Id]; var colours = DiagramAppearance.Colours(chart.Palette);
            var content = new Grid { Margin = new Thickness(11, 7, 11, 7) }; content.RowDefinitions.Add(new() { Height = GridLength.Auto }); content.RowDefinitions.Add(new()); content.RowDefinitions.Add(new() { Height = GridLength.Auto });
            content.Children.Add(new TextBlock { Text = o.Code, FontFamily = new FontFamily("Consolas"), FontSize = 12, Foreground = Colour(DiagramAppearance.Text(chart, n, level)), FontWeight = FontWeights.SemiBold });
            var label = new TextBlock { Text = n.Title, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 1), VerticalAlignment = VerticalAlignment.Top, LineHeight = n.Format.Size * 1.35, LineStackingStrategy = LineStackingStrategy.BlockLineHeight }; FormattingTools.Apply(label, n.Format); label.Foreground = Overdue.IsDue(n.FinishDate, n.Completed) ? OverdueBrush : Colour(DiagramAppearance.Text(chart, n, level)); Grid.SetRow(label, 1); content.Children.Add(label); titleViews[n.Id] = (content, label);
            var preview = string.Join(" ", n.Notes.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var meta = preview.Length > 0 ? preview : n.StartDate.HasValue || n.FinishDate.HasValue ? $"{AustralianDates.Format(n.StartDate)} → {AustralianDates.Format(n.FinishDate)}" : "";
            var note = new TextBlock { Text = meta, FontSize = 12, Foreground = Overdue.IsDue(n.FinishDate, n.Completed) ? OverdueBrush : Colour(DiagramAppearance.Text(chart, n, level)), TextTrimming = TextTrimming.CharacterEllipsis, Visibility = meta.Length == 0 ? Visibility.Collapsed : Visibility.Visible, Margin = new Thickness(0,4,0,0) }; Grid.SetRow(note, 2); content.Children.Add(note);
            var surface = DiagramAppearance.Surface(chart, n, level); var radius = Math.Min(surface.Radius, Math.Min(box.Width,box.Height)/2);
            var decoration = new Border { Background = surface.Decoration is NodeDecoration.Flat or NodeDecoration.Underline ? Brushes.Transparent : Colour(surface.Fill), CornerRadius = new CornerRadius(radius), IsHitTestVisible = false };
            if (surface.Stroke.Length > 0) { decoration.BorderBrush = Colour(surface.Stroke); decoration.BorderThickness = surface.Decoration == NodeDecoration.Box ? new Thickness(1) : surface.Decoration == NodeDecoration.Underline ? new Thickness(0,0,0,2) : surface.Decoration == NodeDecoration.AccentBar ? new Thickness(4,0,0,0) : new Thickness(0); }
            var nodeBody = new Grid(); nodeBody.Children.Add(decoration); nodeBody.Children.Add(content);
            // A constant interaction outline reserves space so selection never changes wrapping.
            var border = new Border { Width = box.Width, Height = box.Height, Background = Brushes.Transparent, BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(radius), Child = nodeBody, Tag = n.Id, ToolTip = n.Title + "\nDouble-click / F2: title. Ctrl+Enter / right-click: details. Drag to move.", Focusable = true };
            AttachNodeInteraction(border, n.Id);
            Canvas.SetLeft(border, box.X); Canvas.SetTop(border, box.Y); canvas.Children.Add(border); nodeViews[n.Id] = border;
        }
        canvas.Width = opening ? scene.Width : Math.Max(canvas.Width, scene.Width); canvas.Height = opening ? scene.Height : Math.Max(canvas.Height, scene.Height); StopZoomAnimation(); SetZoomVisual(chart.Zoom); zoomLabel.Text = $"{chart.Zoom:P0}"; Select(selectedId);
        status.Text = $"{chart.Nodes.Count} nodes · Drag space to pan · Ctrl+wheel to zoom";
        status.ToolTip = "Pan: drag empty space, middle-drag, or Space+drag. Wheel pans vertically; Shift+wheel horizontally. Ctrl+wheel zooms. Ctrl+Enter opens node details.";
        if (opening || centring) { centring = true; CentreCanvas(); }
        // Retain the selected node/nearest existing ancestor in the viewport as the chart reflows.
        if (!networkMode && oldChartId == chart.Id && !centring)
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
        if (id.HasValue && selectedLeadId.HasValue) { selectedLeadId = null; if (networkMode && Current is Diagram graph) DrawLeads(graph, scene.Boxes.ToDictionary(b => b.Id)); }
        formatTools?.Refresh();
        if (Current is not null)
        {
            foreach (var (node, view) in nodeViews) { view.BorderBrush = node == id ? Ui.Accent : Brushes.Transparent; view.BorderThickness = new Thickness(2); }
        }
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
        if (freeDragOriginal is not null) { if (e.Key == Key.Escape) { EndFreeDrag(true); Mouse.Capture(null); } e.Handled = true; return; }
        if (IsInsideTextBox(e.OriginalSource as DependencyObject) || e.IsRepeat && e.Key == Key.Enter) return;
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.Enter) { if (selectedLeadId.HasValue) EditSelectedLead(); else EditNode(); e.Handled = true; }
            else if (e.Key == Key.Z) { Undo(); e.Handled = true; }
            else if (e.Key == Key.Y) { Redo(); e.Handled = true; }
            else if (!networkMode && e.Key == Key.Up && selectedId.HasValue) { Modify(c => Charts.Reorder(c, selectedId.Value, -1)); e.Handled = true; }
            else if (!networkMode && e.Key == Key.Down && selectedId.HasValue) { Modify(c => Charts.Reorder(c, selectedId.Value, 1)); e.Handled = true; }
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        switch (e.Key)
        {
            case Key.Space: e.Handled = true; break;
            case Key.Enter: if (NodeInputPolicy.Enter(false,e.IsRepeat) == NodeEnterAction.CreateNode) AddNode(false); e.Handled = true; break;
            case Key.Insert: AddNode(true); e.Handled = true; break;
            case Key.F2: if (selectedLeadId.HasValue) EditSelectedLead(); else BeginTitle(); e.Handled = true; break;
            case Key.Delete: if (selectedLeadId.HasValue) DeleteSelectedLead(); else DeleteNode(); e.Handled = true; break;
            case Key.Up:
            case Key.Down:
                if (Current is Diagram chart) { var visible = Charts.Outline(chart, true); var index = visible.FindIndex(o => o.Node.Id == selectedId); if (visible.Count > 0) { Select(visible[Math.Clamp(index + (e.Key == Key.Up ? -1 : 1), 0, visible.Count - 1)].Node.Id); ScrollToSelection(); } }
                e.Handled = true; break;
            case Key.Left: if (!networkMode && selectedId.HasValue) ToggleFold(); e.Handled = true; break;
        }
    }
    private void ZoomChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (refreshing || Current is null) return;
        var target = Math.Clamp(zoom.Value,.2,2); var pivot = zoomPivot ?? new Point(viewport.ViewportWidth/2,viewport.ViewportHeight/2);
        AnimateZoom(target,pivot); zoomPending = true; zoomSaveTimer.Stop(); zoomSaveTimer.Start();
    }
    private void ZoomWheel(object sender, MouseWheelEventArgs e)
    {
        if (freeDragOriginal is not null) { e.Handled = true; return; }
        if (Keyboard.Modifiers == ModifierKeys.Control) { zoomPivot = e.GetPosition(viewport); zoom.Value = Math.Clamp(zoom.Value * Math.Pow(1.08,e.Delta / 120d), .2, 2); zoomPivot = null; }
        else if (Keyboard.Modifiers == ModifierKeys.Shift) PanTo(viewport.HorizontalOffset - e.Delta * .6, viewport.VerticalOffset);
        else PanTo(viewport.HorizontalOffset, viewport.VerticalOffset - e.Delta * .6);
        e.Handled = true;
    }
    private void Fit() => FitCanvas();
}
