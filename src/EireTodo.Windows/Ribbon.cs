using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using EireTodo.Core;

namespace EireTodo.Windows;

// Each tab has a fixed-height command band; groups scroll sideways instead of stacking over the canvas.
internal sealed class RibbonBar : Border
{
    private readonly WrapPanel tabs = new();
    private readonly Grid pages = new();
    private readonly Dictionary<string, StackPanel> panels = [];
    private readonly Dictionary<string, Button> buttons = [];
    private readonly Button fold;
    private bool compact;
    private bool? userCollapsed;
    public RibbonBar()
    {
        Background = (Brush)Application.Current.FindResource("PanelBrush"); BorderBrush = (Brush)Application.Current.FindResource("LineBrush"); BorderThickness = new Thickness(1); Margin = new Thickness(0, 0, 0, 8);
        var root = new DockPanel(); var heading = new DockPanel(); DockPanel.SetDock(heading, Dock.Top);
        fold = Ui.Button("⌃", (_, _) => { userCollapsed = pages.Visibility == Visibility.Visible; ApplyCollapse(); }); fold.ToolTip = "Collapse / expand ribbon. Double-click a tab to toggle."; fold.Width = 36; fold.MinHeight = 32; fold.Padding = new Thickness(0); DockPanel.SetDock(fold, Dock.Right); heading.Children.Add(fold); heading.Children.Add(tabs); root.Children.Add(heading); root.Children.Add(pages); Child = root;
        foreach (var name in new[] { "Home", "Insert", "Format", "View" })
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 8, 2, 4) }; panels[name] = panel;
            var scroller = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Visibility = Visibility.Collapsed, Tag = name }; pages.Children.Add(scroller);
            scroller.PreviewMouseWheel += (_, e) => { if (Keyboard.Modifiers == ModifierKeys.Shift) { scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta); e.Handled = true; } };
            var button = Ui.Button(name, (_, _) => { if (pages.Visibility != Visibility.Visible) { userCollapsed = false; ApplyCollapse(); } Show(name); }); button.FontSize = 15; button.MinHeight = 32; button.Padding = new Thickness(18, 4, 18, 4); button.BorderThickness = new Thickness(0, 0, 0, 2);
            button.PreviewMouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { userCollapsed = pages.Visibility == Visibility.Visible; ApplyCollapse(); e.Handled = true; } };
            buttons[name] = button; tabs.Children.Add(button);
        }
        Show("Home");
    }
    public void SetCompact(bool value) { compact = value; ApplyCollapse(); }
    private void ApplyCollapse()
    {
        var collapsed = userCollapsed ?? compact; pages.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible; fold.Content = collapsed ? "⌄" : "⌃";
    }
    public void Show(string name)
    {
        foreach (FrameworkElement page in pages.Children) page.Visibility = (string)page.Tag == name ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (key, button) in buttons) { button.Foreground = key == name ? Ui.Accent : Brushes.White; button.BorderBrush = key == name ? Ui.Accent : Brushes.Transparent; }
    }
    public WrapPanel Group(string tab, string caption)
    {
        var content = new DockPanel(); var label = new TextBlock { Text = caption.ToUpperInvariant(), FontSize = 12, Foreground = Brushes.White, Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Center }; DockPanel.SetDock(label, Dock.Bottom); content.Children.Add(label);
        var controls = new WrapPanel { Orientation = Orientation.Vertical, Height = 80 }; content.Children.Add(controls);
        panels[tab].Children.Add(new Border { Child = content, BorderBrush = (Brush)Application.Current.FindResource("LineBrush"), BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(0, 0, 12, 0), Margin = new Thickness(0, 0, 12, 0) }); return controls;
    }
    public static void ShowGroup(Panel panel, bool visible)
    {
        var group = (panel.Parent as FrameworkElement)?.Parent as FrameworkElement;
        if (group is not null) group.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public static void Prepare(Control control)
    {
        control.FontSize = 15; control.MinHeight = 34; control.Margin = new Thickness(0, 0, 6, 4);
        if (control is Button button) button.Padding = new Thickness(10, 4, 10, 4);
    }
    public static Button Action(Panel panel, string label, Action action, bool primary = false)
    {
        var button = Ui.Button(label, (_, _) => action(), primary); Prepare(button); panel.Children.Add(button); return button;
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
        var fonts = new WrapPanel(); family = new ComboBox { Width = 190, FontSize = 15, MinHeight = 34, ToolTip = "Font family", ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).Where(s => s.IndexOfAny(['/', '\\', ':', '#']) < 0).Order().ToList(), IsTextSearchEnabled = true };
        size = new ComboBox { Width = 76, FontSize = 15, MinHeight = 34, ToolTip = "Font size (12–48)", ItemsSource = new double[] { 12, 14, 15, 16, 17, 18, 20, 22, 24, 28, 32, 36, 40, 44, 48 }, IsEditable = true, Margin = new Thickness(6, 0, 6, 0) };
        fonts.Children.Add(family); fonts.Children.Add(size); Children.Add(fonts);
        var styles = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var box in new[] { bold, italic, underline }) { box.Margin = new Thickness(0, 0, 10, 0); styles.Children.Add(box); box.Checked += (_, _) => Commit(); box.Unchecked += (_, _) => Commit(); }
        align = new ComboBox { Width = 105, FontSize = 15, MinHeight = 34, ToolTip = "Text alignment", ItemsSource = new[] { "Left", "Centre", "Right" }, Margin = new Thickness(0, 0, 8, 0) }; styles.Children.Add(align);
        painterButton = RibbonBar.Action(styles, "Format painter", () => { if (painter.Armed) painter.Clear(); else if (selected() is TextFormat value) painter.Copy(value); });
        painterButton.ToolTip = "Copy the selected item's formatting, then click a task or node to apply. Click again to cancel.";
        painter.Changed += () => { painterButton.Content = painter.Armed ? "Paint next item · cancel" : "Format painter"; painterButton.BorderBrush = painter.Armed ? Ui.Accent : (Brush)Application.Current.FindResource("LineBrush"); };
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
