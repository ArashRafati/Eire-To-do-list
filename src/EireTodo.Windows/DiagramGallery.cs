using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using EireTodo.Core;

namespace EireTodo.Windows;

internal enum DiagramGalleryKind { Styles, Colours, Layouts }

// Visible, real shape/colour previews in the ribbon; an expandable keyboard-accessible gallery.
internal sealed class DiagramGallery : StackPanel
{
    private readonly DiagramGalleryKind kind;
    private readonly Action<int> choose;
    private readonly StackPanel strip = new() { Orientation = Orientation.Horizontal };
    private readonly TextBlock caption = new() { FontSize = 13, Foreground = Ui.Ink, Margin = new Thickness(0, 3, 0, 0) };
    private readonly Popup popup = new() { StaysOpen = false, AllowsTransparency = true, Placement = PlacementMode.Bottom };
    private DiagramPalette palette;
    private NodeDesign design;
    private ChartLayout layout = ChartLayout.MindMap;
    private int selected;
    private readonly int count;

    internal DiagramGallery(DiagramGalleryKind kind, Action<int> choose)
    {
        this.kind = kind; this.choose = choose;
        count = kind == DiagramGalleryKind.Styles ? Enum.GetValues<NodeDesign>().Length : kind == DiagramGalleryKind.Colours ? Enum.GetValues<DiagramPalette>().Length : Enum.GetValues<ChartLayout>().Count(l => l != ChartLayout.Freeform);
        Width = 354; Height = 84; Margin = new Thickness(0, 0, 4, 0);
        Children.Add(strip); Children.Add(caption);
        Unloaded += (_, _) => popup.IsOpen = false;
        IsEnabledChanged += (_, _) => { if (!IsEnabled) popup.IsOpen = false; };
        Refresh(null);
    }

    internal void Refresh(Diagram? diagram)
    {
        IsEnabled = diagram is not null;
        palette = diagram?.Palette ?? DiagramPalette.Eire;
        design = diagram?.Design ?? NodeDesign.Tiered;
        layout = diagram?.Kind == DiagramKind.Network ? ChartLayout.TopDown : diagram?.Layout ?? ChartLayout.MindMap;
        selected = kind == DiagramGalleryKind.Styles ? (int)design : kind == DiagramGalleryKind.Colours ? (int)palette : (int)layout;
        caption.Text = diagram is null ? "Create a diagram to choose a style" : ItemName(selected) + " · " + count + " options";
        strip.Children.Clear();
        // Show useful alternatives immediately, as well as the current choice.
        var featured = kind == DiagramGalleryKind.Styles ? new[] { selected, (int)NodeDesign.Flat, (int)NodeDesign.Underlined } : kind == DiagramGalleryKind.Colours ? new[] { selected, (int)DiagramPalette.Navy, (int)DiagramPalette.Plum } : new[] { selected, (int)ChartLayout.MindMap, (int)ChartLayout.TopDown };
        foreach (var index in featured.Concat(Enumerable.Range(0, count)).Distinct().Take(3)) strip.Children.Add(Tile(index, 100, true));
        var more = new Button { Content = "▾", Width = 34, Height = 64, Style = (Style)Application.Current.FindResource("RibbonButton"), Padding = new Thickness(2), Margin = new Thickness(2, 0, 0, 0), ToolTip = kind == DiagramGalleryKind.Colours ? "Show all 16 colour combinations" : kind == DiagramGalleryKind.Layouts ? "Show all diagram layouts" : "Show all node designs" };
        AutomationProperties.SetName(more, "More " + kind.ToString().ToLowerInvariant());
        more.Click += (_, _) => Open(more); strip.Children.Add(more);
        if (popup.IsOpen) popup.IsOpen = false;
    }

    private string ItemName(int index) => kind == DiagramGalleryKind.Styles ? DiagramAppearance.DesignName((NodeDesign)index) : kind == DiagramGalleryKind.Colours ? DiagramAppearance.Colours((DiagramPalette)index).Name : Charts.LayoutName((ChartLayout)index);
    private Button Tile(int index, double width, bool compact = false)
    {
        var label = ItemName(index);
        var panel = new StackPanel();
        panel.Children.Add(new DiagramThumbnail(kind == DiagramGalleryKind.Colours ? (DiagramPalette)index : palette, kind == DiagramGalleryKind.Styles ? (NodeDesign)index : design, kind == DiagramGalleryKind.Layouts ? (ChartLayout)index : layout) { Width = width - 12, Height = compact ? 30 : 50 });
        panel.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = Ui.Ink, TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0), MaxWidth = width - 12 });
        var button = new Button { Content = panel, Width = width, Height = compact ? 64 : 84, Padding = new Thickness(4), Margin = new Thickness(0, 0, 4, 0), Style = (Style)Application.Current.FindResource("RibbonButton"), BorderThickness = new Thickness(1), BorderBrush = index == selected ? Ui.Accent : Brushes.Transparent, Background = index == selected ? Ui.Brush("SoftTeal") : Brushes.Transparent, ToolTip = label + "\n" + (kind == DiagramGalleryKind.Styles ? DiagramAppearance.DesignDescription((NodeDesign)index) : kind == DiagramGalleryKind.Colours ? "Apply to all nodes and connections; priority and overdue highlights remain visible." : "Arrange all visible nodes using this layout.") };
        AutomationProperties.SetName(button, label); AutomationProperties.SetItemStatus(button, index == selected ? "Selected" : "Available");
        ToolTipService.SetInitialShowDelay(button, 400);
        button.Click += (_, _) => { popup.IsOpen = false; choose(index); };
        return button;
    }

    internal Button CompactButton()
    {
        var button = new Button { Style = (Style)Application.Current.FindResource("RibbonButton"), Width = 30, Height = 28, Padding = new Thickness(4) };
        RibbonBar.Label(button,kind == DiagramGalleryKind.Colours ? "Colour combinations" : kind == DiagramGalleryKind.Layouts ? "Diagram layouts" : "Node designs");
        if (button.Content is StackPanel row) row.Children.RemoveAt(row.Children.Count - 1);
        button.SetBinding(IsEnabledProperty,new System.Windows.Data.Binding("IsEnabled") { Source = this }); button.Click += (_, _) => Open(button); return button;
    }
    private void Open(Button anchor)
    {
        var columns = Math.Clamp((int)((SystemParameters.WorkArea.Width - 68) / 142), 1, 4);
        var wrap = new WrapPanel { Width = columns * 142, Margin = new Thickness(8) };
        KeyboardNavigation.SetDirectionalNavigation(wrap, KeyboardNavigationMode.Cycle);
        Button? current = null;
        for (var i = 0; i < count; i++) { var button = Tile(i, 138); if (i == selected) current = button; wrap.Children.Add(button); }
        var title = new TextBlock { Text = kind == DiagramGalleryKind.Colours ? "Colour combinations" : kind == DiagramGalleryKind.Layouts ? "Diagram layouts" : "Node designs", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(12, 10, 12, 4), Foreground = Ui.Ink, TextWrapping = TextWrapping.Wrap };
        var body = new DockPanel(); DockPanel.SetDock(title, Dock.Top); body.Children.Add(title);
        body.Children.Add(new ScrollViewer { Content = wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, MaxHeight = Math.Max(160, Math.Min(460, SystemParameters.WorkArea.Height - 180)) });
        popup.Child = new Border { Child = body, Background = Ui.Brush("Surface"), BorderBrush = Ui.Brush("LineBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5), MaxWidth = Math.Max(180, SystemParameters.WorkArea.Width - 24) };
        popup.PlacementTarget = anchor; popup.IsOpen = true;
        popup.Child.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { popup.IsOpen = false; anchor.Focus(); e.Handled = true; } };
        if (current is not null) current.Dispatcher.InvokeAsync(() => { current.Focus(); current.BringIntoView(); });
    }
}

internal sealed class DiagramThumbnail : FrameworkElement
{
    private readonly Diagram chart;
    private readonly ChartScene scene;
    internal DiagramThumbnail(DiagramPalette palette, NodeDesign design, ChartLayout layout)
    {
        chart = DiagramSamples.Create(layout, palette, design);
        scene = ChartGeometry.Arrange(chart, measure: (_, _) => (96, 42));
        IsHitTestVisible = false;
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var scale = Math.Min((ActualWidth - 4) / scene.Width, (ActualHeight - 4) / scene.Height);
        if (scale <= 0) return;
        dc.PushTransform(new TranslateTransform((ActualWidth - scene.Width * scale) / 2, (ActualHeight - scene.Height * scale) / 2));
        dc.PushTransform(new ScaleTransform(scale, scale));
        var boxes = scene.Boxes.ToDictionary(b => b.Id); var levels = DiagramAppearance.Levels(chart);
        var pen = new Pen(Brush(DiagramAppearance.Colours(chart.Palette).Lead), 2 / scale);
        foreach (var node in chart.Nodes.Where(n => n.ParentId.HasValue))
        {
            var c = ChartGeometry.Connector(boxes[node.ParentId!.Value], boxes[node.Id], chart.Layout);
            var geometry = new StreamGeometry(); using (var g = geometry.Open())
            {
                g.BeginFigure(new(c.X1, c.Y1), false, false);
                if (chart.Layout is ChartLayout.MindMap or ChartLayout.RightTree) g.BezierTo(new((c.X1 + c.X2) / 2, c.Y1), new((c.X1 + c.X2) / 2, c.Y2), new(c.X2, c.Y2), true, false);
                else if (chart.Layout == ChartLayout.TopDown) { g.LineTo(new(c.X1, (c.Y1 + c.Y2) / 2), true, false); g.LineTo(new(c.X2, (c.Y1 + c.Y2) / 2), true, false); g.LineTo(new(c.X2, c.Y2), true, false); }
                else { g.LineTo(new((c.X1 + c.X2) / 2, c.Y1), true, false); g.LineTo(new((c.X1 + c.X2) / 2, c.Y2), true, false); g.LineTo(new(c.X2, c.Y2), true, false); }
            }
            dc.DrawGeometry(null, pen, geometry);
        }
        foreach (var box in scene.Boxes)
        {
            var surface = DiagramAppearance.Surface(chart, chart.Nodes.Single(n => n.Id == box.Id), levels[box.Id]);
            var stroke = surface.Stroke.Length > 0 ? new Pen(Brush(surface.Stroke), 1 / scale) : null;
            var radius = Math.Min(surface.Radius, box.Height / 2);
            if (surface.Decoration is NodeDecoration.Box or NodeDecoration.AccentBar) dc.DrawRoundedRectangle(Brush(surface.Fill), surface.Decoration == NodeDecoration.Box ? stroke : null, new(box.X, box.Y, box.Width, box.Height), radius, radius);
            if (surface.Decoration == NodeDecoration.Underline) dc.DrawLine(stroke, new(box.X, box.Y + box.Height), new(box.X + box.Width, box.Y + box.Height));
            if (surface.Decoration == NodeDecoration.AccentBar) dc.DrawLine(new Pen(Brush(surface.Stroke), 3 / scale), new(box.X, box.Y), new(box.X, box.Y + box.Height));
            var text = new Pen(Brush(surface.Text), 1.4 / scale);
            dc.DrawLine(text, new(box.X + 12, box.Y + 16), new(box.X + box.Width - 12, box.Y + 16));
            dc.DrawLine(text, new(box.X + 12, box.Y + 27), new(box.X + box.Width - 28, box.Y + 27));
        }
        dc.Pop(); dc.Pop();
    }
    private static Brush Brush(string colour) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(colour));
}
