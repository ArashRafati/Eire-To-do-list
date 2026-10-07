using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed partial class ChartWorkspace
{
    private readonly FormatPainter painter;
    private readonly Action changed;
    private bool networkMode;
    private WrapPanel hierarchyControls = null!, leadControls = null!, unlinkedControls = null!, leadOptions = null!, layoutControls = null!;
    private Button siblingButton = null!, childButton = null!, fromTodoButton = null!;
    private FormattingTools formatTools = null!;
    private readonly ComboBox leadDirection = new() { ItemsSource = new[] { "Outgoing →", "Incoming ←", "Both ↔" }, SelectedIndex = 0, Width = 150, MinHeight = 34, FontSize = 15, Margin = new Thickness(0, 0, 6, 2), ToolTip = "Direction for new leads and connected nodes" };
    private LeadDirection Direction => (LeadDirection)Math.Max(0, leadDirection.SelectedIndex);
    private readonly ComboBox leadRouting = new() { ItemsSource = new[] { "Curve", "Sharp bends" }, SelectedIndex = 0, Width = 150, MinHeight = 34, FontSize = 15, Margin = new Thickness(0, 0, 6, 4) };
    private LeadRouting Routing => leadRouting.SelectedIndex == 1 ? LeadRouting.SharpBends : LeadRouting.Curve;
    private Guid? connectFrom;
    private readonly Dictionary<Guid, (Grid Grid, TextBlock Label)> titleViews = [];
    private readonly List<UIElement> leadViews = [];
    private static readonly Brush OverdueBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(BrandTheme.Error));
    private TextBox? inline;
    private Guid? inlineId;
    private Diagram? inlineBefore;
    private bool inlineSaved, endingInline;
    private readonly DispatcherTimer titleTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private const string DragFormat = "Eire.ChartNode";
    private sealed record NodeDrag(Guid Diagram, Guid Node);

    private TextFormat? SelectedFormat() => Current?.Nodes.FirstOrDefault(n => n.Id == selectedId)?.Format;
    private void ApplyFormat(TextFormat format)
    {
        if (selectedId is not Guid id) return; Modify(c => c.Nodes.Single(n => n.Id == id).Format = format.Clone(), false);
    }
    private void BeginTitle()
    {
        if (!FinishInlineEdit() || selectedId is not Guid id || Current is not Diagram chart || !titleViews.TryGetValue(id, out var view)) return;
        inlineId = id; inlineBefore = chart.Clone(); inlineSaved = false;
        var node = chart.Nodes.Single(n => n.Id == id);
        inline = new TextBox { Text = node.Title, MaxLength = 100, FontFamily = new FontFamily(node.Format.Family), FontSize = node.Format.Size, FontWeight = node.Format.Bold ? FontWeights.Bold : FontWeights.Normal, FontStyle = node.Format.Italic ? FontStyles.Italic : FontStyles.Normal, TextAlignment = node.Format.Alignment == TextJustification.Centre ? TextAlignment.Center : node.Format.Alignment == TextJustification.Right ? TextAlignment.Right : TextAlignment.Left, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, MinHeight = 0, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(2), VerticalAlignment = VerticalAlignment.Top };
        view.Label.Visibility = Visibility.Collapsed; Grid.SetRow(inline, 1); view.Grid.Children.Add(inline);
        var editor = inline;
        editor.TextChanged += (_, _) => { status.Text = "Saving title…"; titleTimer.Stop(); titleTimer.Start(); };
        titleTimer.Tick -= SaveTitleTick; titleTimer.Tick += SaveTitleTick;
        editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { titleTimer.Stop(); if (!FinishInlineEdit(true)) return; viewport.Focus(); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { if (FinishInlineEdit()) EditNode(); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { if (NodeInputPolicy.Enter(true,e.IsRepeat) == NodeEnterAction.ConfirmTitle && FinishInlineEdit()) { RefreshData(); viewport.Focus(); } e.Handled = true; }
            else if (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None) { if (FinishInlineEdit()) AddNode(true); e.Handled = true; }
            else if (e.Key == Key.Tab) { if (FinishInlineEdit()) viewport.Focus(); e.Handled = true; }
        };
        editor.LostKeyboardFocus += (_, _) => { if (!endingInline && ReferenceEquals(inline, editor)) FinishInlineEdit(); };
        Dispatcher.InvokeAsync(() => { if (ReferenceEquals(inline, editor)) { editor.Focus(); editor.SelectAll(); editor.BringIntoView(); } }, DispatcherPriority.Loaded);
    }
    private void SaveTitleTick(object? sender, EventArgs e)
    {
        titleTimer.Stop(); if (inline is not null && !string.IsNullOrWhiteSpace(inline.Text)) SaveInlineTitle();
    }
    private bool SaveInlineTitle()
    {
        if (inline is null || inlineId is not Guid id || Current is not Diagram chart) return true;
        var text = inline.Text.Trim(); if (text.Length == 0) { status.Text = "Enter a title. The previously saved title is retained."; inline.BorderBrush = OverdueBrush; return false; }
        if (chart.Nodes.Single(n => n.Id == id).Title == text) return true;
        try
        {
            var candidate = chart.Clone(); candidate.Nodes.Single(n => n.Id == id).Title = text; service.SaveDiagram(candidate);
            if (!inlineSaved && inlineBefore is not null) { undo.Push(inlineBefore); redo.Clear(); LimitHistory(); inlineSaved = true; }
            inline.BorderBrush = Ui.Accent; changed(); status.Text = networkMode ? "Title saved · Enter / Insert: linked node · Ctrl+Enter: details" : "Title saved · Enter: sibling · Insert: child · Ctrl+Enter: details"; return true;
        }
        catch (Exception ex) { status.Text = "Title NOT saved. Keep this field open and retry: " + ex.Message; inline.BorderBrush = OverdueBrush; return false; }
    }
    public bool FinishInlineEdit(bool discardPending = false)
    {
        if (!FlushZoom()) return false;
        if (inline is null || endingInline) return true;
        titleTimer.Stop(); if (!discardPending && !SaveInlineTitle()) { inline.Focus(); return false; }
        endingInline = true;
        if (inlineId is Guid id && titleViews.TryGetValue(id, out var view))
        {
            view.Label.Text = Current?.Nodes.FirstOrDefault(n => n.Id == id)?.Title ?? view.Label.Text; view.Label.Visibility = Visibility.Visible; view.Grid.Children.Remove(inline);
        }
        var reflow = inlineSaved; var version = renderVersion;
        inline = null; inlineId = null; inlineBefore = null; endingInline = false;
        if (reflow) Dispatcher.InvokeAsync(() => { if (inline is null && renderVersion == version) RefreshData(); }, DispatcherPriority.Background);
        return true;
    }
    private void StartConnection()
    {
        if (!FinishInlineEdit() || !networkMode || selectedId is not Guid id) { status.Text = "Select the source node first."; return; }
        connectFrom = id; status.Text = "Click a target node to connect. Direction and routing use the ribbon choices.";
    }
    private bool ClickNode(Guid id)
    {
        if (!FinishInlineEdit()) return false;
        if (networkMode && connectFrom is Guid from)
        {
            if (id == from) { status.Text = "Choose a different target node."; return false; }
            Modify(c => GraphCreation.Connect(c, from, id, Direction, Routing), false); connectFrom = null; Select(id); return false;
        }
        Select(id);
        if (painter.Copied is TextFormat format)
        {
            if (Current is Diagram chart && Try(() => { var candidate = chart.Clone(); candidate.Nodes.Single(n => n.Id == id).Format = format.Clone(); Commit(candidate); })) { painter.Clear(); RefreshData(); }
            return false;
        }
        return true;
    }
    private void AttachNodeInteraction(Border border, Guid id)
    {
        Point? pressed = null; Point original = default; bool moved = false;
        border.MouseLeftButtonDown += (_, e) =>
        {
            if (e.OriginalSource is TextBox || IsInsideTextBox(e.OriginalSource as DependencyObject)) return;
            if (!ClickNode(id)) { e.Handled = true; return; }
            viewport.Focus();
            if (e.ClickCount == 2) { BeginTitle(); e.Handled = true; return; }
            pressed = e.GetPosition(viewport); original = networkMode ? new Point(Canvas.GetLeft(border) - canvasOrigin.X, Canvas.GetTop(border) - canvasOrigin.Y) : new Point(Canvas.GetLeft(border), Canvas.GetTop(border)); moved = false;
            border.CaptureMouse(); e.Handled = true;
        };
        border.MouseMove += (_, e) =>
        {
            if (pressed is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(viewport); var scale = displayZoom; var dx = (p.X - start.X) / scale; var dy = (p.Y - start.Y) / scale;
            if (!moved && Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance) return;
            moved = true;
            if (!networkMode)
            {
                pressed = null; border.ReleaseMouseCapture(); if (Current is Diagram d) { ShowInsertionSlots(id); try { DragDrop.DoDragDrop(border, new DataObject(DragFormat, new NodeDrag(d.Id, id)), DragDropEffects.Move); } finally { ClearInsertionSlots(); } } Render(); return;
            }
            var worldX = Math.Clamp(original.X + dx, -100000, 100000); var worldY = Math.Clamp(original.Y + dy, -100000, 100000); MakeWorldPointVisible(worldX, worldY, border.Width, border.Height); var x = worldX + canvasOrigin.X; var y = worldY + canvasOrigin.Y; Canvas.SetLeft(border, x); Canvas.SetTop(border, y);
            var boxes = scene.Boxes.Select(b => b.Id == id ? b with { X = x, Y = y } : b).ToDictionary(b => b.Id);
            if (Current is Diagram chart) DrawLeads(chart, boxes); canvas.Width = Math.Max(canvas.Width, x + border.Width + OpenWorkspace.Padding); canvas.Height = Math.Max(canvas.Height, y + border.Height + OpenWorkspace.Padding);
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            if (pressed.HasValue) { pressed = null; border.ReleaseMouseCapture(); if (networkMode && moved) { var x = Canvas.GetLeft(border) - canvasOrigin.X; var y = Canvas.GetTop(border) - canvasOrigin.Y; Modify(c => NetworkCharts.Move(c, id, x, y)); } e.Handled = true; }
            pressed = null;
        };
        border.LostMouseCapture += (_, _) => { if (pressed.HasValue && networkMode) { pressed = null; if (moved) Render(); } };
        border.AllowDrop = !networkMode;
        NodeDrop Position(DragEventArgs e) { var point = e.GetPosition(border); var fraction = Current?.Layout == ChartLayout.TopDown ? point.X / border.ActualWidth : point.Y / border.ActualHeight; return fraction < .28 ? NodeDrop.Before : fraction > .72 ? NodeDrop.After : NodeDrop.Child; }
        border.DragOver += (_, e) =>
        {
            var drag = e.Data.GetData(DragFormat) as NodeDrag;
            var valid = Current is Diagram chart && drag?.Diagram == chart.Id && drag.Node != id && !Charts.Descendants(chart, drag.Node).Contains(id);
            e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true;
            var point = e.GetPosition(viewport);
            if (point.X < 35) viewport.ScrollToHorizontalOffset(viewport.HorizontalOffset - 15); else if (point.X > viewport.ActualWidth - 35) viewport.ScrollToHorizontalOffset(viewport.HorizontalOffset + 15);
            if (point.Y < 35) viewport.ScrollToVerticalOffset(viewport.VerticalOffset - 15); else if (point.Y > viewport.ActualHeight - 35) viewport.ScrollToVerticalOffset(viewport.VerticalOffset + 15);
            if (valid) { var drop = Position(e); border.BorderBrush = Ui.Accent; border.BorderThickness = drop == NodeDrop.Before ? new Thickness(1, 5, 1, 1) : drop == NodeDrop.After ? new Thickness(1, 1, 1, 5) : new Thickness(4); status.Text = drop == NodeDrop.Child ? "Drop: move branch inside this node" : "Drop: move branch " + (drop == NodeDrop.Before ? "before" : "after") + " this node"; }
        };
        border.DragLeave += (_, _) => Select(selectedId);
        border.Drop += (_, e) =>
        {
            if (e.Data.GetData(DragFormat) is NodeDrag drag && Current?.Id == drag.Diagram) { selectedId = drag.Node; var drop = Position(e); Modify(c => Charts.MoveRelative(c, drag.Node, id, drop)); ScrollToSelection(); } e.Handled = true;
        };
        var menu = new ContextMenu();
        void Item(string text, Action action) { var item = new MenuItem { Header = text }; item.Click += (_, _) => { Select(id); action(); }; menu.Items.Add(item); }
        Item("Node details · Ctrl+Enter", EditNode); Item("Edit title · F2", BeginTitle);
        if (networkMode) Item("Connect from this node…", StartConnection);
        else { Item("Move before / after…", MoveSelected); Item("Add sibling · Enter", () => AddNode(false)); Item("Add child · Insert", () => AddNode(true)); }
        Item("Delete node", DeleteNode); border.ContextMenu = menu;
        border.PreviewMouseRightButtonDown += (_, e) => { if (!FinishInlineEdit()) e.Handled = true; else Select(id); };
    }
    private static bool IsInsideTextBox(DependencyObject? source)
    {
        for (var p = source; p is not null; p = p is Visual ? VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p)) if (p is TextBox) return true;
        return false;
    }
}
