using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed partial class ChartWorkspace
{
    private readonly System.Windows.Controls.Canvas zoomSurface = new();
    private readonly ScaleTransform zoomTransform = new(1,1);
    private readonly DispatcherTimer zoomSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private double displayZoom = 1, animationStartZoom, animationTarget;
    private Point animationPivot, animationAnchor;
    private DateTime animationStarted;
    private bool zoomAnimating, zoomPending;
    private void InitialiseSmoothZoom()
    {
        canvas.RenderTransform = zoomTransform; zoomSurface.Children.Add(canvas); viewport.Content = zoomSurface;
        canvas.SizeChanged += (_,_) => UpdateCanvasExtent();
        zoomSaveTimer.Tick += (_,_) => { zoomSaveTimer.Stop(); FlushZoom(); };
        Unloaded += (_,_) => StopZoomAnimation();
    }
    private void UpdateCanvasExtent() { zoomSurface.Width = Math.Max(1,canvas.Width * displayZoom); zoomSurface.Height = Math.Max(1,canvas.Height * displayZoom); }
    private void SetZoomVisual(double value)
    {
        displayZoom = value; zoomTransform.ScaleX = zoomTransform.ScaleY = value; UpdateCanvasExtent(); zoomLabel.Text = $"{value:P0}";
    }
    private void StopZoomAnimation() { if (!zoomAnimating) return; CompositionTarget.Rendering -= ZoomFrame; zoomAnimating = false; }
    private void AnimateZoom(double target,Point pivot)
    {
        StopZoomAnimation(); animationStartZoom = displayZoom; animationTarget = target; animationPivot = pivot;
        animationAnchor = new((viewport.HorizontalOffset+pivot.X)/displayZoom,(viewport.VerticalOffset+pivot.Y)/displayZoom);
        animationStarted = DateTime.UtcNow; zoomAnimating = true; CompositionTarget.Rendering += ZoomFrame;
    }
    private void ZoomFrame(object? sender,EventArgs e)
    {
        var progress = Math.Clamp((DateTime.UtcNow-animationStarted).TotalMilliseconds/130,0,1); var eased = 1-Math.Pow(1-progress,3);
        SetZoomVisual(animationStartZoom+(animationTarget-animationStartZoom)*eased); zoomSurface.UpdateLayout();
        viewport.ScrollToHorizontalOffset(animationAnchor.X*displayZoom-animationPivot.X); viewport.ScrollToVerticalOffset(animationAnchor.Y*displayZoom-animationPivot.Y);
        if (progress >= 1) StopZoomAnimation();
    }
    private bool FlushZoom()
    {
        if (!zoomPending || Current is not Diagram chart) return true;
        zoomSaveTimer.Stop();
        try { var copy = chart.Clone(); copy.Zoom = Math.Clamp(zoom.Value,.2,2); service.SaveDiagram(copy); zoomPending = false; return true; }
        catch (Exception ex) { status.Text = "Zoom setting NOT saved: " + ex.Message; return false; }
    }
}
