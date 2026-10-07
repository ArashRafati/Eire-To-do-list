using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EireTodo.Core;

namespace EireTodo.Windows;

// A compact tabbed ribbon that retains the app's existing dark/yellow palette.
internal sealed class RibbonBar : Border
{
    private readonly WrapPanel tabs = new();
    private readonly Grid pages = new();
    private readonly Dictionary<string, WrapPanel> panels = [];
    private readonly Dictionary<string, Button> buttons = [];
    public RibbonBar()
    {
        Background = (Brush)Application.Current.FindResource("PanelBrush"); BorderBrush = (Brush)Application.Current.FindResource("LineBrush"); BorderThickness = new Thickness(1); Margin = new Thickness(0, 0, 0, 10);
        var root = new DockPanel(); DockPanel.SetDock(tabs, Dock.Top); root.Children.Add(tabs); root.Children.Add(pages); Child = root;
        foreach (var name in new[] { "Home", "Insert", "Format", "View" })
        {
            var panel = new WrapPanel { Margin = new Thickness(8), Visibility = Visibility.Collapsed }; panels[name] = panel; pages.Children.Add(panel);
            var button = Ui.Button(name, (_, _) => Show(name)); button.MinHeight = 34; button.Padding = new Thickness(14, 4, 14, 4); button.BorderThickness = new Thickness(0, 0, 0, 2); buttons[name] = button; tabs.Children.Add(button);
        }
        Show("Home");
    }
    public void Show(string name)
    {
        foreach (var (key, panel) in panels) { panel.Visibility = key == name ? Visibility.Visible : Visibility.Collapsed; buttons[key].Foreground = key == name ? Ui.Accent : Brushes.White; buttons[key].BorderBrush = key == name ? Ui.Accent : Brushes.Transparent; }
    }
    public WrapPanel Group(string tab, string caption)
    {
        var stack = new StackPanel(); var controls = new WrapPanel(); stack.Children.Add(controls);
        stack.Children.Add(new TextBlock { Text = caption.ToUpperInvariant(), FontSize = 12, Foreground = Brushes.White, Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
        panels[tab].Children.Add(new Border { Child = stack, BorderBrush = (Brush)Application.Current.FindResource("LineBrush"), BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(0, 0, 10, 0), Margin = new Thickness(0, 0, 10, 6) }); return controls;
    }
    public static void ShowGroup(Panel panel, bool visible)
    {
        var group = (panel.Parent as FrameworkElement)?.Parent as FrameworkElement;
        if (group is not null) group.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
    public static Button Action(Panel panel, string label, Action action, bool primary = false)
    {
        var button = Ui.Button(label, (_, _) => action(), primary); button.FontSize = 15; button.Margin = new Thickness(0, 0, 6, 0); button.MinHeight = 38; panel.Children.Add(button); return button;
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
        var fonts = new WrapPanel(); family = new ComboBox { Width = 190, ItemsSource = Fonts.SystemFontFamilies.Select(f => f.Source).Where(s => s.IndexOfAny(['/', '\\', ':', '#']) < 0).Order().ToList(), IsTextSearchEnabled = true };
        size = new ComboBox { Width = 76, ItemsSource = new double[] { 12, 14, 15, 16, 17, 18, 20, 22, 24, 28, 32, 36, 40, 44, 48 }, IsEditable = true, Margin = new Thickness(6, 0, 6, 0) };
        fonts.Children.Add(family); fonts.Children.Add(size); Children.Add(fonts);
        var styles = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        foreach (var box in new[] { bold, italic, underline }) { box.Margin = new Thickness(0, 0, 10, 0); styles.Children.Add(box); box.Checked += (_, _) => Commit(); box.Unchecked += (_, _) => Commit(); }
        align = new ComboBox { Width = 105, ItemsSource = new[] { "Left", "Centre", "Right" }, Margin = new Thickness(0, 0, 8, 0) }; styles.Children.Add(align);
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
