using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Data;
using EireTodo.Core;

namespace EireTodo.Windows;

// Always-visible tabs and an unclipped, horizontally scrollable command band.
internal sealed class RibbonBar : Border
{
    private readonly StackPanel tabs = new() { Orientation = Orientation.Horizontal };
    private readonly Grid pages = new();
    private readonly Dictionary<string, StackPanel> panels = [];
    private readonly Dictionary<string, Button> buttons = [];
    private readonly Button fold;
    private readonly StackPanel summary = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(6,2,6,2) };
    private readonly ScrollViewer summaryScroll;
    private readonly List<GroupState> groups = [];
    private string scope = "Shared", activeScope = "Todo", activeTab = "Home";
    private sealed class GroupState
    {
        public required Border View { get; init; }
        public required WrapPanel Controls { get; init; }
        public required string Scope { get; init; }
        public required string Tab { get; init; }
        public bool RequestedVisible = true, ScopeVisible = true;
        public List<FrameworkElement> Shortcuts { get; } = [];
    }
    private bool compact;
    private bool? userCollapsed;
    public RibbonBar()
    {
        Background = (Brush)Application.Current.FindResource("PanelBrush"); BorderBrush = (Brush)Application.Current.FindResource("LineBrush"); BorderThickness = new Thickness(0,0,0,1); Margin = new Thickness(0, 0, 0, 4);
        var root = new DockPanel(); var heading = new DockPanel(); DockPanel.SetDock(heading, Dock.Top);
        summaryScroll = new ScrollViewer { Content = summary, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Visibility = Visibility.Collapsed }; DockPanel.SetDock(summaryScroll, Dock.Top);
        fold = Ui.Button("⌃", (_, _) => { userCollapsed = pages.Visibility == Visibility.Visible; ApplyCollapse(); }); fold.Style = (Style)Application.Current.FindResource("RibbonButton"); fold.ToolTip = "Compact commands / full ribbon. Double-click a tab to toggle."; fold.Width = 30; fold.MinHeight = 28; fold.Padding = new Thickness(0); DockPanel.SetDock(fold, Dock.Right); heading.Children.Add(fold); heading.Children.Add(tabs); root.Children.Add(heading); root.Children.Add(summaryScroll); root.Children.Add(pages); Child = root;
        foreach (var name in new[] { "Home", "Insert", "Format", "View" })
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 4, 2, 2) }; panels[name] = panel;
            var scroller = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Visibility = Visibility.Collapsed, Tag = name }; pages.Children.Add(scroller);
            scroller.PreviewMouseWheel += (_, e) => { if (Keyboard.Modifiers == ModifierKeys.Shift) { scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta); e.Handled = true; } };
            var button = Ui.Button(name, (_, _) => Show(name)); button.Style = (Style)Application.Current.FindResource("RibbonButton"); button.FontSize = 14; button.MinHeight = 30; button.Padding = new Thickness(9, 3, 9, 3); button.ToolTip = name + " commands"; button.BorderThickness = new Thickness(0, 0, 0, 2);
            button.PreviewMouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { userCollapsed = pages.Visibility == Visibility.Visible; ApplyCollapse(); e.Handled = true; } };
            buttons[name] = button; tabs.Children.Add(button);
        }
        Show("Home");
    }
    public void SetCompact(bool value) { compact = value; ApplyCollapse(); }
    private void ApplyCollapse()
    {
        var collapsed = userCollapsed ?? compact; pages.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible; summaryScroll.Visibility = collapsed ? Visibility.Visible : Visibility.Collapsed; fold.Content = collapsed ? "⌄" : "⌃"; if (collapsed) BuildSummary();
    }
    public void Show(string name)
    {
        activeTab = name;
        foreach (FrameworkElement page in pages.Children) page.Visibility = (string)page.Tag == name ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (key, button) in buttons) { button.Style = (Style)Application.Current.FindResource(key == name ? "RibbonTabSelected" : "RibbonButton"); button.BorderBrush = key == name ? Ui.Accent : Brushes.Transparent; }
        if (summaryScroll.Visibility == Visibility.Visible) BuildSummary();
    }
    public void BeginScope(string value) => scope = value;
    public void SetScope(string value)
    {
        activeScope = value;
        foreach (var group in groups) { group.ScopeVisible = group.Scope == "Shared" || group.Scope == value || group.Scope == "Charts" && value != "Todo"; group.View.Visibility = group.ScopeVisible && group.RequestedVisible ? Visibility.Visible : Visibility.Collapsed; }
        if (summaryScroll.Visibility == Visibility.Visible) BuildSummary();
    }
    public void AddShortcut(Panel panel, FrameworkElement command)
    {
        var state = groups.Single(g => g.Controls == panel); state.Shortcuts.Add(command);
    }
    private void BuildSummary()
    {
        summary.Children.Clear();
        foreach (var group in groups.Where(g => g.Tab == activeTab && g.ScopeVisible && g.RequestedVisible))
        {
            if (summary.Children.Count > 0) summary.Children.Add(new Border { BorderBrush = Ui.Brush("LineBrush"), BorderThickness = new Thickness(1,0,0,0), Margin = new Thickness(5,3,5,3) });
            foreach (var custom in group.Shortcuts) { if (custom.Parent is Panel old) old.Children.Remove(custom); summary.Children.Add(custom); }
            if (group.Shortcuts.Count > 0) continue;
            foreach (var selector in Descendants<ComboBox>(group.Controls).Where(c => !InsideFormatting(c)))
            {
                var button = new Button { Style = (Style)Application.Current.FindResource("RibbonButton"), Width = 30, Height = 28, Padding = new Thickness(4) };
                Label(button,selector.ToolTip as string ?? "Choose diagram / option");
                if (button.Content is StackPanel row) row.Children.RemoveAt(row.Children.Count - 1);
                button.Click += (_, _) => { var menu = new ContextMenu(); foreach (var value in selector.Items) { var item = new MenuItem { Header = value is Diagram d ? d.Name : value.ToString(), IsCheckable = true, IsChecked = Equals(value,selector.SelectedItem) }; item.Click += (_, _) => selector.SelectedItem = value; menu.Items.Add(item); } menu.PlacementTarget = button; menu.IsOpen = true; };
                button.SetBinding(IsEnabledProperty,new Binding("IsEnabled") { Source = selector }); summary.Children.Add(button);
            }
            foreach (var gallery in Descendants<DiagramGallery>(group.Controls)) summary.Children.Add(gallery.CompactButton());
            foreach (var source in Descendants<Button>(group.Controls).Where(b => b.Visibility != Visibility.Collapsed && b.Content is not DiagramThumbnail && !InsideGallery(b)))
            {
                var label = System.Windows.Automation.AutomationProperties.GetName(source); if (label.Length == 0) label = source.Content as string ?? source.ToolTip as string ?? "Command";
                var button = new Button { Style = (Style)Application.Current.FindResource("RibbonButton"), Width = 30, Height = 28, Padding = new Thickness(4), Margin = new Thickness(1,0,1,0), ToolTip = source.ToolTip ?? label };
                Label(button, label); if (button.Content is StackPanel content) content.Children.RemoveAt(content.Children.Count - 1);
                button.SetBinding(IsEnabledProperty,new Binding("IsEnabled") { Source = source }); button.Click += (_, _) => source.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); summary.Children.Add(button);
            }
            foreach (var source in Descendants<CheckBox>(group.Controls).Where(c => c.Visibility != Visibility.Collapsed))
            {
                var command = new CheckBox { Style = (Style)Application.Current.FindResource("RibbonCommandToggle"), Content = source.Content is string caption && caption.Length <= 1 ? caption : source.Content is string toggle && toggle.Contains("Free") ? "↔" : "⌖", ToolTip = source.ToolTip ?? source.Content, MinHeight = 28, Margin = new Thickness(3,0,3,0) };

                command.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, new Binding("IsChecked") { Source = source, Mode = BindingMode.TwoWay }); command.SetBinding(IsEnabledProperty,new Binding("IsEnabled") { Source = source }); summary.Children.Add(command);
            }
            if (Descendants<FormattingTools>(group.Controls).Any()) Action(summary,"Fonts",() => { userCollapsed = false; ApplyCollapse(); Show("Format"); });
        }
    }
    private static bool InsideFormatting(DependencyObject element)
    {
        for (var parent = LogicalTreeHelper.GetParent(element); parent is not null; parent = LogicalTreeHelper.GetParent(parent)) if (parent is FormattingTools) return true;
        return false;
    }
    private static bool InsideGallery(DependencyObject element)
    {
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null; parent = VisualTreeHelper.GetParent(parent)) if (parent is DiagramGallery) return true;
        return false;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        IEnumerable<DependencyObject> children = parent is Panel panel ? panel.Children.Cast<DependencyObject>() : parent is Decorator decorator && decorator.Child is not null ? new[] { decorator.Child } : parent is ContentControl control && control.Content is DependencyObject content ? new[] { content } : Enumerable.Empty<DependencyObject>();
        foreach (var child in children) { if (child is T item) yield return item; foreach (var nested in Descendants<T>(child)) yield return nested; }
    }
    public WrapPanel Group(string tab, string caption)
    {
        var content = new DockPanel(); var label = new TextBlock { Text = caption.ToUpperInvariant(), FontSize = 11, Foreground = Ui.Ink, Margin = new Thickness(0, 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Left }; DockPanel.SetDock(label, Dock.Bottom); content.Children.Add(label);
        var controls = new WrapPanel { Orientation = Orientation.Vertical, Height = 84 }; content.Children.Add(controls);
        var border = new Border { Child = content, BorderBrush = (Brush)Application.Current.FindResource("LineBrush"), BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(0, 0, 9, 0), Margin = new Thickness(0, 0, 9, 0) };
        var state = new GroupState { View = border, Controls = controls, Scope = scope, Tab = tab }; border.Tag = state; groups.Add(state); panels[tab].Children.Add(border); SetScope(activeScope); return controls;
    }
    public static void ShowGroup(Panel panel, bool visible)
    {
        var group = (panel.Parent as FrameworkElement)?.Parent as FrameworkElement;
        if (group is Border { Tag: GroupState state }) { state.RequestedVisible = visible; group.Visibility = state.RequestedVisible && state.ScopeVisible ? Visibility.Visible : Visibility.Collapsed; }
        else if (group is not null) group.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public static void Prepare(Control control)
    {
        control.FontSize = 14; control.MinHeight = 28; control.Margin = new Thickness(0, 0, 5, 3); control.HorizontalAlignment = HorizontalAlignment.Left;
        if (control is Button button) { button.Style = (Style)Application.Current.FindResource("RibbonButton"); button.Padding = new Thickness(5, 3, 5, 3); if (button.Content is string caption) Label(button,caption); }
    }
    public static void Label(Button button,string caption)
    {
        var text = caption.Replace("_", "").Replace("+ ", ""); var lower = text.ToLowerInvariant();
        var motif = lower.Contains("delete") ? "M5,6 L19,6 M8,6 L8,20 L17,20 L17,6 M10,3 L15,3 M10,10 L10,17 M14,10 L14,17" :
            lower.Contains("undo") || lower.Contains("earlier") ? "M8,5 L3,10 L8,15 M3,10 L15,10 Q22,10 20,19" :
            lower.Contains("redo") || lower.Contains("later") ? "M16,5 L21,10 L16,15 M21,10 L9,10 Q2,10 4,19" :
            lower.Contains("font") || lower.Contains("format") || lower.Contains("paint") ? "M4,4 L19,4 L19,10 L4,10 Z M15,10 L15,14 L11,14 L11,21" :
            lower.Contains("export") ? "M4,10 L4,21 L20,21 L20,10 M12,16 L12,2 M7,7 L12,2 L17,7" :
            lower.Contains("connect") || lower.Contains("lead") || lower.Contains("linked") ? "M2,3 L8,3 L8,9 L2,9 Z M16,15 L22,15 L22,21 L16,21 Z M8,6 L12,6 L12,18 L16,18 M13,15 L16,18 L13,21" :
            lower.Contains("fit") || lower.Contains("centre") ? "M8,3 L3,3 L3,8 M16,3 L21,3 L21,8 M3,16 L3,21 L8,21 M21,16 L21,21 L16,21 M8,12 L16,12 M12,8 L12,16" :
            lower.Contains("edit") || lower.Contains("title") || lower.Contains("label") ? "M4,16 L4,21 L9,21 L21,9 L16,4 Z M14,6 L19,11" :
            lower.Contains("new") || lower.Contains("add") || lower.Contains("sibling") || lower.Contains("child") ? "M3,4 L17,4 L17,20 L3,20 Z M12,12 L22,12 M17,7 L17,17" :
            lower.Contains("setting") || lower.Contains("project") ? "M3,5 L10,5 L12,8 L21,8 L21,20 L3,20 Z M7,12 L17,12 M7,16 L14,16" :
            lower.Contains("log") || lower.Contains("detail") ? "M5,3 L19,3 L19,21 L5,21 Z M9,8 L15,8 M9,12 L15,12 M9,16 L13,16" :
            lower.Contains("fold") || lower.Contains("indent") ? "M3,5 L21,5 M9,11 L21,11 M9,17 L21,17 M2,10 L6,13 L2,16" :
            lower.Contains("filter") ? "M3,4 L21,4 L14,12 L14,20 L10,18 L10,12 Z" : "M4,4 L20,4 L20,20 L4,20 Z M8,8 L16,8 M8,12 L16,12 M8,16 L12,16";
        var icon = new System.Windows.Shapes.Path { Data = Geometry.Parse(motif), Width = 16, Height = 16, Stretch = Stretch.Uniform, StrokeThickness = 1.25, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Margin = new Thickness(0,0,5,0) };
        icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground") { Source = button });
        var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(icon); row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center }); button.Content = row;
        button.ToolTip = text + TooltipDetail(lower); System.Windows.Automation.AutomationProperties.SetName(button,text); ToolTipService.SetInitialShowDelay(button,450); ToolTipService.SetShowDuration(button,20000);
    }
    private static string TooltipDetail(string text) => text.Contains("delete node") ? "\nRemove the selected node and its branch immediately. Undo restores it." : text.Contains("delete lead") ? "\nRemove the selected lead. Nodes stay in place. Undo restores it." : text.Contains("sibling") ? "\nEnter outside title editing creates a node at the same level." : text.Contains("child") ? "\nInsert creates a node below the selection." : text.Contains("linked") ? "\nEnter / Insert creates a node linked from the graph selection." : text.Contains("lead label") ? "\nSelect a lead, then edit its label, direction and endpoints. F2 also opens this editor." : text.Contains("fit") ? "\nFit and centre the entire diagram in the workspace." : text.Contains("format painter") ? "\nCopy the selected text style, then click the destination task or node." : text.Contains("export") ? "\nExport the full diagram. PDF opens a paper preview first." : text.Contains("details") ? "\nEdit the full notes, dates and status. Shortcut: Ctrl+Enter." : "\n" + text + " for the current selection or diagram.";
    public static Button Action(Panel panel, string label, Action action, bool primary = false)
    {
        var button = Ui.Button(label, (_, _) => action(), primary); Prepare(button); if (primary) button.Style = (Style)Application.Current.FindResource("RibbonPrimaryButton"); panel.Children.Add(button); return button;
    }
}

internal sealed class FormatPainter
{
    public TextFormat? Copied { get; private set; }
    public bool Armed => Copied is not null;
    public event Action? Changed;
    public void Copy(TextFormat format) { Copied = format.Clone(); Changed?.Invoke(); }
    public void Clear() { Copied = null; Changed?.Invoke(); }
}

internal sealed class FormattingTools : StackPanel
{
    private readonly ComboBox family, size;
    private readonly CheckBox bold = new() { Content = "B", FontWeight = FontWeights.Bold }, italic = new() { Content = "I", FontStyle = FontStyles.Italic }, underline = new() { Content = "U" };
    private readonly ComboBox align;
    private readonly Button painterButton;
    private readonly Action<TextFormat> apply;
    private readonly Func<TextFormat?> selected;
    private bool loading;
    public FormattingTools(Func<TextFormat?> selected, Action<TextFormat> apply, FormatPainter painter)
    {
        this.apply = apply; this.selected = selected;
        var fonts = new WrapPanel(); family = new ComboBox { Width = 190, FontSize = 14, MinHeight = 28, ToolTip = "Font family", ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).Where(s => s.IndexOfAny(['/', '\\', ':', '#']) < 0).Order().ToList(), IsTextSearchEnabled = true };
        size = new ComboBox { Width = 76, FontSize = 14, MinHeight = 28, ToolTip = "Font size (12–48)", ItemsSource = new double[] { 12, 14, 15, 16, 17, 18, 20, 22, 24, 28, 32, 36, 40, 44, 48 }, IsEditable = true, Margin = new Thickness(6, 0, 6, 0) };
        fonts.Children.Add(family); fonts.Children.Add(size); Children.Add(fonts);
        var styles = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        foreach (var box in new[] { bold, italic, underline }) { box.Style = (Style)Application.Current.FindResource("RibbonToggle"); box.ToolTip = box == bold ? "Bold" : box == italic ? "Italic" : "Underline"; box.Margin = new Thickness(0, 0, 5, 0); styles.Children.Add(box); box.Checked += (_, _) => Commit(); box.Unchecked += (_, _) => Commit(); }
        align = new ComboBox { Width = 105, FontSize = 14, MinHeight = 28, ToolTip = "Text alignment", ItemsSource = new[] { "Left", "Centre", "Right" }, Margin = new Thickness(0, 0, 8, 0) }; styles.Children.Add(align);
        painterButton = RibbonBar.Action(styles, "Format painter", () => { if (painter.Armed) painter.Clear(); else if (selected() is TextFormat value) painter.Copy(value); });
        painterButton.ToolTip = "Copy the selected item's formatting, then click a task or node to apply. Click again to cancel.";
        painter.Changed += () => { RibbonBar.Label(painterButton,painter.Armed ? "Paint next item · cancel" : "Format painter"); painterButton.BorderBrush = painter.Armed ? Ui.Accent : (Brush)Application.Current.FindResource("LineBrush"); };
        RibbonBar.Action(styles, "Reset", () => { if (selected() is not null) apply(new()); }); Children.Add(styles);
        family.SelectionChanged += (_, _) => Commit(); size.SelectionChanged += (_, _) => Commit(); align.SelectionChanged += (_, _) => Commit();
        size.LostKeyboardFocus += (_, _) => Commit(); size.PreviewKeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { Commit(); e.Handled = true; } };
        Refresh();
    }
    private void Commit()
    {
        if (loading || selected() is null) return;
        var value = size.SelectedItem is double choice ? choice : double.TryParse(size.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : double.NaN;
        if (!double.IsFinite(value) || value < 12 || value > 48) return;
        var format = new TextFormat { Family = family.SelectedItem as string ?? "Segoe UI", Size = value, Bold = bold.IsChecked == true, Italic = italic.IsChecked == true, Underline = underline.IsChecked == true, Alignment = (TextJustification)Math.Max(0, align.SelectedIndex) };
        apply(format);
    }
    public void Refresh()
    {
        loading = true; var f = selected() ?? new(); family.SelectedItem = f.Family; size.SelectedItem = f.Size; size.Text = f.Size.ToString(CultureInfo.InvariantCulture); bold.IsChecked = f.Bold; italic.IsChecked = f.Italic; underline.IsChecked = f.Underline; align.SelectedIndex = (int)f.Alignment; IsEnabled = selected() is not null; loading = false;
    }
    public static void Apply(TextBlock label, TextFormat f)
    {
        label.FontFamily = new FontFamily(f.Family); label.FontSize = f.Size; label.FontWeight = f.Bold ? FontWeights.Bold : FontWeights.Normal; label.FontStyle = f.Italic ? FontStyles.Italic : FontStyles.Normal;
        label.TextDecorations = f.Underline ? TextDecorations.Underline : null; label.TextAlignment = f.Alignment == TextJustification.Centre ? TextAlignment.Center : f.Alignment == TextJustification.Right ? TextAlignment.Right : TextAlignment.Left;
    }
}
