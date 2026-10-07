using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed partial class ChartWorkspace
{
    private Point canvasOrigin = new(OpenWorkspace.Padding, OpenWorkspace.Padding);
    private ChartScene documentScene = new([], 500, 300);
    private SceneBounds documentBounds = new(0, 0, 0, 0);
    private Point? panStart;
    private Point panOffsets;
    private Point? zoomPivot;
    private bool centring;

    private void InitialiseOpenCanvas()
    {
        viewport.HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;
        viewport.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Hidden;
        viewport.PanningMode = System.Windows.Controls.PanningMode.Both;
        viewport.PreviewMouseDown += (_, e) =>
        {
            var pan = e.ChangedButton == MouseButton.Middle || e.ChangedButton == MouseButton.Left && (Keyboard.IsKeyDown(Key.Space) || ReferenceEquals(e.OriginalSource, canvas));
            if (!pan || IsInsideTextBox(e.OriginalSource as DependencyObject)) return;
            e.Handled = true; if (!FinishInlineEdit()) return;
            viewport.Focus(); panStart = e.GetPosition(viewport); panOffsets = new(viewport.HorizontalOffset, viewport.VerticalOffset); viewport.Cursor = Cursors.Hand; viewport.CaptureMouse();
        };
        viewport.MouseMove += (_, e) =>
        {
            if (panStart is not Point start) return;
            var pointer = e.GetPosition(viewport); var x = panOffsets.X + start.X - pointer.X; var y = panOffsets.Y + start.Y - pointer.Y;
            var shift = PanTo(x, y); panOffsets = new(panOffsets.X + shift.X, panOffsets.Y + shift.Y); e.Handled = true;
        };
        viewport.PreviewMouseUp += (_, e) => { if (panStart.HasValue) { panStart = null; viewport.ReleaseMouseCapture(); viewport.Cursor = Cursors.Arrow; e.Handled = true; } };
        viewport.LostMouseCapture += (_, _) => { panStart = null; viewport.Cursor = Cursors.Arrow; };
        viewport.SizeChanged += (_, _) => { if (centring) CentreCanvas(); };
    }
    private Point PanTo(double x, double y)
    {
        var scale = displayZoom; double dx = 0, dy = 0;
        if (x < 512 * scale) dx = OpenWorkspace.Padding;
        if (y < 512 * scale) dy = OpenWorkspace.Padding;
        if (dx > 0 || dy > 0) { ShiftCanvasOrigin(dx, dy); x += dx * scale; y += dy * scale; }
        canvas.Width = Math.Max(canvas.Width, (x + viewport.ViewportWidth) / scale + OpenWorkspace.Padding);
        canvas.Height = Math.Max(canvas.Height, (y + viewport.ViewportHeight) / scale + OpenWorkspace.Padding);
        UpdateCanvasExtent(); zoomSurface.UpdateLayout(); viewport.ScrollToHorizontalOffset(x); viewport.ScrollToVerticalOffset(y);
        return new(dx * scale, dy * scale);
    }
    private void ShiftCanvasOrigin(double dx, double dy)
    {
        canvasOrigin = new(canvasOrigin.X + dx, canvasOrigin.Y + dy);
        scene = scene with { Boxes = scene.Boxes.Select(b => b with { X = b.X + dx, Y = b.Y + dy }).ToList(), Width = scene.Width + dx, Height = scene.Height + dy };
        foreach (var view in nodeViews.Values) { System.Windows.Controls.Canvas.SetLeft(view, System.Windows.Controls.Canvas.GetLeft(view) + dx); System.Windows.Controls.Canvas.SetTop(view, System.Windows.Controls.Canvas.GetTop(view) + dy); }
        foreach (var path in canvas.Children.OfType<System.Windows.Shapes.Path>().Where(p => !leadViews.Contains(p)))
        {
            var x = System.Windows.Controls.Canvas.GetLeft(path); var y = System.Windows.Controls.Canvas.GetTop(path);
            System.Windows.Controls.Canvas.SetLeft(path, (double.IsNaN(x) ? 0 : x) + dx); System.Windows.Controls.Canvas.SetTop(path, (double.IsNaN(y) ? 0 : y) + dy);
        }
        canvas.Width += dx; canvas.Height += dy;
        if (networkMode && Current is Diagram chart) DrawLeads(chart, scene.Boxes.ToDictionary(b => b.Id));
    }
    private void MakeWorldPointVisible(double x, double y, double width, double height)
    {
        var oldOffset = new Point(viewport.HorizontalOffset, viewport.VerticalOffset); var scale = displayZoom;
        var dx = x + canvasOrigin.X < 512 ? OpenWorkspace.Padding + 512 - x - canvasOrigin.X : 0;
        var dy = y + canvasOrigin.Y < 512 ? OpenWorkspace.Padding + 512 - y - canvasOrigin.Y : 0;
        if (dx > 0 || dy > 0) ShiftCanvasOrigin(dx, dy);
        canvas.Width = Math.Max(canvas.Width, x + canvasOrigin.X + width + OpenWorkspace.Padding);
        canvas.Height = Math.Max(canvas.Height, y + canvasOrigin.Y + height + OpenWorkspace.Padding);
        if (dx > 0 || dy > 0) { UpdateCanvasExtent(); zoomSurface.UpdateLayout(); viewport.ScrollToHorizontalOffset(oldOffset.X + dx * scale); viewport.ScrollToVerticalOffset(oldOffset.Y + dy * scale); }
    }
    private void PrepareOpenScene(Diagram chart, bool opening)
    {
        documentScene = ChartGeometry.Arrange(chart, measure: WindowsNodeMetrics.Measure); documentBounds = OpenWorkspace.Bounds(chart, documentScene);
        if (opening) canvasOrigin = new(OpenWorkspace.Padding + Math.Max(0, -documentBounds.X), OpenWorkspace.Padding + Math.Max(0, -documentBounds.Y));
        var dx = Math.Max(0, OpenWorkspace.Padding - documentBounds.X - canvasOrigin.X);
        var dy = Math.Max(0, OpenWorkspace.Padding - documentBounds.Y - canvasOrigin.Y);
        if (!opening && (dx > 0 || dy > 0))
        {
            canvasOrigin = new(canvasOrigin.X + dx, canvasOrigin.Y + dy);
            var scale = displayZoom;
            var x = viewport.HorizontalOffset + dx * scale; var y = viewport.VerticalOffset + dy * scale;
            Dispatcher.InvokeAsync(() => { viewport.ScrollToHorizontalOffset(x); viewport.ScrollToVerticalOffset(y); }, DispatcherPriority.Loaded);
        }
        scene = OpenWorkspace.ToCanvas(documentScene, canvasOrigin.X, canvasOrigin.Y);
    }
    private void CentreCanvas()
    {
        var version = renderVersion;
        Dispatcher.InvokeAsync(() =>
        {
            if (version != renderVersion || Current is not Diagram chart) return;
            var offsets = OpenWorkspace.Centre(documentBounds, canvasOrigin.X, canvasOrigin.Y, displayZoom, viewport.ViewportWidth, viewport.ViewportHeight);
            viewport.ScrollToHorizontalOffset(offsets.X); viewport.ScrollToVerticalOffset(offsets.Y); centring = false;
        }, DispatcherPriority.Loaded);
    }
    private void FitCanvas()
    {
        if (Current is null || !FinishInlineEdit()) return;
        zoom.Value = Math.Clamp(Math.Min((viewport.ViewportWidth - 48) / Math.Max(1, documentBounds.Width), (viewport.ViewportHeight - 48) / Math.Max(1, documentBounds.Height)), .2, 2);
        if (!FlushZoom()) return; StopZoomAnimation(); SetZoomVisual(zoom.Value); centring = true; CentreCanvas(); viewport.Focus();
    }
}
