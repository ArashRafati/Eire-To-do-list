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
    private WrapPanel hierarchyControls = null!, leadControls = null!;
    private Button siblingButton = null!, childButton = null!, fromTodoButton = null!;
    private FormattingTools formatTools = null!;
    private readonly CheckBox twoHeads = new() { Content = "Double lead", Margin = new Thickness(4, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox leadRouting = new() { ItemsSource = new[] { "Curve", "Sharp bends" }, SelectedIndex = 0, Width = 150 };
    private LeadRouting Routing => leadRouting.SelectedIndex == 1 ? LeadRouting.SharpBends : LeadRouting.Curve;
    private Guid? connectFrom;
    private readonly Dictionary<Guid, (Grid Grid, TextBlock Label)> titleViews = [];
    private readonly List<UIElement> leadViews = [];
    private static readonly Brush OverdueBrush = new SolidColorBrush(Color.FromRgb(255, 91, 91));
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
        inline = new TextBox { Text = node.Title, MaxLength = 100, FontFamily = new FontFamily(node.Format.Family), FontSize = node.Format.Size, FontWeight = node.Format.Bold ? FontWeights.Bold : FontWeights.Normal, FontStyle = node.Format.Italic ? FontStyles.Italic : FontStyles.Normal, TextAlignment = node.Format.Alignment == TextJustification.Centre ? TextAlignment.Center : node.Format.Alignment == TextJustification.Right ? TextAlignment.Right : TextAlignment.Left, TextWrapping = TextWrapping.Wrap, AcceptsReturn = false, Padding = new Thickness(2), VerticalAlignment = VerticalAlignment.Top };
        view.Label.Visibility = Visibility.Collapsed; Grid.SetRow(inline, 1); view.Grid.Children.Add(inline);
        var editor = inline;
        editor.TextChanged += (_, _) => { status.Text = "Saving title… · Enter: sibling · Insert: child · Ctrl+Enter: details"; titleTimer.Stop(); titleTimer.Start(); };
        titleTimer.Tick -= SaveTitleTick; titleTimer.Tick += SaveTitleTick;
        editor.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { titleTimer.Stop(); if (!FinishInlineEdit(true)) return; viewport.Focus(); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { if (FinishInlineEdit()) EditNode(); e.Handled = true; }
            else if ((e.Key == Key.Enter || e.Key == Key.Insert) && Keyboard.Modifiers == ModifierKeys.None) { if (FinishInlineEdit()) AddNode(e.Key == Key.Insert); e.Handled = true; }
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
            inline.BorderBrush = Ui.Accent; changed(); status.Text = "Title saved · Enter: sibling · Insert: child · Ctrl+Enter: details"; return true;
        }
        catch (Exception ex) { status.Text = "Title NOT saved. Keep this field open and retry: " + ex.Message; inline.BorderBrush = OverdueBrush; return false; }
    }
    public bool FinishInlineEdit(bool discardPending = false)
    {
        if (inline is null || endingInline) return true;
        titleTimer.Stop(); if (!discardPending && !SaveInlineTitle()) { inline.Focus(); return false; }
        endingInline = true;
        if (inlineId is Guid id && titleViews.TryGetValue(id, out var view))
        {
            view.Label.Text = Current?.Nodes.FirstOrDefault(n => n.Id == id)?.Title ?? view.Label.Text; view.Label.Visibility = Visibility.Visible; view.Grid.Children.Remove(inline);
        }
        inline = null; inlineId = null; inlineBefore = null; endingInline = false; return true;
    }
    private void StartConnection()
    {
        if (!FinishInlineEdit() || !networkMode || selectedId is not Guid id) { status.Text = "Select the source node first."; return; }
        connectFrom = id; status.Text = "Click a target node to connect. Double lead and routing use the ribbon choices.";
    }
    private bool ClickNode(Guid id)
    {
        if (!FinishInlineEdit()) return false;
        if (networkMode && connectFrom is Guid from)
        {
            if (id == from) { status.Text = "Choose a different target node."; return false; }
            Modify(c => NetworkCharts.Connect(c, from, id, twoHeads.IsChecked == true, Routing), false); connectFrom = null; Select(id); return false;
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
            pressed = e.GetPosition(canvas); original = new Point(Canvas.GetLeft(border), Canvas.GetTop(border)); moved = false;
            if (networkMode) border.CaptureMouse(); e.Handled = true;
        };
        border.MouseMove += (_, e) =>
        {
            if (pressed is not Point start || e.LeftButton != MouseButtonState.Pressed) return;
            var p = e.GetPosition(canvas); var dx = p.X - start.X; var dy = p.Y - start.Y;
            if (!moved && Math.Abs(dx) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(dy) < SystemParameters.MinimumVerticalDragDistance) return;
            moved = true;
            if (!networkMode)
            {
                pressed = null; if (Current is Diagram d) DragDrop.DoDragDrop(border, new DataObject(DragFormat, new NodeDrag(d.Id, id)), DragDropEffects.Move); Render(); return;
            }
            var x = Math.Clamp(original.X + dx, 40, 100000); var y = Math.Clamp(original.Y + dy, 40, 100000); Canvas.SetLeft(border, x); Canvas.SetTop(border, y);
            var boxes = scene.Boxes.Select(b => b.Id == id ? b with { X = x, Y = y } : b).ToDictionary(b => b.Id);
            if (Current is Diagram chart) DrawLeads(chart, boxes); canvas.Width = Math.Max(scene.Width, x + border.Width + 40); canvas.Height = Math.Max(scene.Height, y + border.Height + 40);
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            if (networkMode && pressed.HasValue) { pressed = null; border.ReleaseMouseCapture(); if (moved) { var x = Canvas.GetLeft(border); var y = Canvas.GetTop(border); Modify(c => NetworkCharts.Move(c, id, x, y)); } e.Handled = true; }
            pressed = null;
        };
        border.LostMouseCapture += (_, _) => { if (pressed.HasValue && networkMode) { pressed = null; if (moved) Render(); } };
        border.AllowDrop = !networkMode;
        NodeDrop Position(DragEventArgs e) { var fraction = e.GetPosition(border).Y / border.ActualHeight; return fraction < .28 ? NodeDrop.Before : fraction > .72 ? NodeDrop.After : NodeDrop.Child; }
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
        else { Item("Add sibling · Enter", () => AddNode(false)); Item("Add child · Insert", () => AddNode(true)); }
        Item("Delete node", DeleteNode); border.ContextMenu = menu;
        border.PreviewMouseRightButtonDown += (_, e) => { if (!FinishInlineEdit()) e.Handled = true; else Select(id); };
    }
    private static bool IsInsideTextBox(DependencyObject? source)
    {
        for (var p = source; p is not null; p = p is Visual ? VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p)) if (p is TextBox) return true;
        return false;
    }
    private void DrawLeads(Diagram chart, Dictionary<Guid, NodeBox> boxes)
    {
        foreach (var view in leadViews) canvas.Children.Remove(view); leadViews.Clear();
        var groups = chart.Leads.GroupBy(l => l.From.CompareTo(l.To) < 0 ? (l.From, l.To) : (l.To, l.From));
        foreach (var group in groups)
        {
            var list = group.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                var lead = list[i]; if (!boxes.TryGetValue(lead.From, out var from) || !boxes.TryGetValue(lead.To, out var to)) continue;
                var c = LeadGeometry.Route(from, to, lead.Routing, i - list.Count / 2, boxes.Values.ToList());
                var geometry = new StreamGeometry(); using (var g = geometry.Open()) { g.BeginFigure(new Point(c.X1, c.Y1), false, false); if (lead.Routing == LeadRouting.Curve) g.BezierTo(new Point(c.C1X, c.C1Y), new Point(c.C2X, c.C2Y), new Point(c.X2, c.Y2), true, false); else { g.LineTo(new Point(c.C1X, c.C1Y), true, false); g.LineTo(new Point(c.C2X, c.C2Y), true, false); g.LineTo(new Point(c.X2, c.Y2), true, false); } } geometry.Freeze();
                var hit = new System.Windows.Shapes.Path { Data = geometry, Stroke = Brushes.Transparent, StrokeThickness = 14, ToolTip = "Right-click or double-click to edit lead" };
                void Edit() => EditLead(lead.Id);
                hit.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Edit(); e.Handled = true; };
                var menu = new ContextMenu(); var entry = new MenuItem { Header = "Lead description / direction / shape" }; entry.Click += (_, _) => Edit(); menu.Items.Add(entry); hit.ContextMenu = menu;
                Put(hit); Put(new System.Windows.Shapes.Path { Data = geometry, Stroke = new SolidColorBrush(Color.FromRgb(105, 133, 161)), StrokeThickness = 2, IsHitTestVisible = false });
                Arrow(c.X2, c.Y2, c.C2X, c.C2Y); if (lead.DoubleHeaded) Arrow(c.X1, c.Y1, c.C1X, c.C1Y);
                if (!string.IsNullOrWhiteSpace(lead.Description))
                {
                    var text = new TextBlock { Text = lead.Description, FontSize = 15, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = c.LabelWidth, ToolTip = lead.Description };
                    var label = new Border { Child = text, Background = (Brush)Application.Current.FindResource("CanvasBrush"), Padding = new Thickness(5, 2, 5, 2), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(c.LabelAngle) };
                    label.Measure(new Size(c.LabelWidth + 10, double.PositiveInfinity)); Canvas.SetLeft(label, c.LabelX - label.DesiredSize.Width / 2); Canvas.SetTop(label, c.LabelY - label.DesiredSize.Height / 2);
                    label.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Edit(); e.Handled = true; }; label.ContextMenu = menu; Put(label);
                }
            }
        }
        void Put(UIElement view) { Panel.SetZIndex(view, -1); canvas.Children.Add(view); leadViews.Add(view); }
        void Arrow(double x, double y, double previousX, double previousY)
        {
            var angle = Math.Atan2(y - previousY, x - previousX); var back = angle + Math.PI;
            Put(new Polygon { Fill = Ui.Accent, IsHitTestVisible = false, Points = [new(x, y), new(x + 13 * Math.Cos(back + .42), y + 13 * Math.Sin(back + .42)), new(x + 13 * Math.Cos(back - .42), y + 13 * Math.Sin(back - .42))] });
        }
    }
    private void EditLead(Guid id)
    {
        if (!FinishInlineEdit() || Current is not Diagram chart) return;
        var lead = chart.Leads.Single(l => l.Id == id); var dialog = new Window { Title = "Edit lead", Width = 570, Height = 450, MinWidth = 440, MinHeight = 350, MaxHeight = Math.Max(350, SystemParameters.WorkArea.Height - 32), Owner = OwnerWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner }; Ui.ApplyWindowStyle(dialog);
        var panel = new StackPanel { Margin = new Thickness(18) }; var description = new TextBox { Text = lead.Description, MaxLength = 500 };
        var heads = new CheckBox { Content = "Double lead · arrows at both ends", IsChecked = lead.DoubleHeaded, Margin = new Thickness(0, 12, 0, 12) };
        var route = new ComboBox { ItemsSource = new[] { "Curve", "Straight segments with sharp bends" }, SelectedIndex = (int)lead.Routing };
        var reverse = new CheckBox { Content = "Reverse the lead direction", Margin = new Thickness(0, 12, 0, 12) };
        panel.Children.Add(Ui.Label(chart.Nodes.Single(n => n.Id == lead.From).Title + " → " + chart.Nodes.Single(n => n.Id == lead.To).Title)); panel.Children.Add(Ui.Field("Optional lead description", description)); panel.Children.Add(heads); panel.Children.Add(Ui.Field("Lead shape", route)); panel.Children.Add(reverse);
        var buttons = new WrapPanel(); RibbonBar.Action(buttons, "Save lead", () => { if (Try(() => { var candidate = chart.Clone(); var l = candidate.Leads.Single(x => x.Id == id); l.Description = description.Text.Trim(); l.DoubleHeaded = heads.IsChecked == true; l.Routing = (LeadRouting)route.SelectedIndex; if (reverse.IsChecked == true) (l.From, l.To) = (l.To, l.From); Commit(candidate); })) { dialog.DialogResult = true; RefreshData(); } }, true);
        RibbonBar.Action(buttons, "Delete lead", () => { if (MessageBox.Show(dialog, "Delete this lead?", "Confirm deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes && Try(() => { var candidate = chart.Clone(); candidate.Leads.RemoveAll(l => l.Id == id); Commit(candidate); })) { dialog.DialogResult = true; RefreshData(); } });
        panel.Margin = new Thickness(0); buttons.Margin = new Thickness(0, 12, 0, 0);
        var root = new DockPanel { Margin = new Thickness(18) }; DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons); root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); dialog.Content = root; dialog.ShowDialog();
    }
}
