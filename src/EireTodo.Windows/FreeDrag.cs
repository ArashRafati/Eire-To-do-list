using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed partial class ChartWorkspace
{
    private readonly CheckBox freeMoveToggle = new() { Content = "Free move", MinHeight = 28, ToolTip = "Move any node without changing its hierarchy. Nearby nodes make room. Turn off to drag branches between levels." };
    private WrapPanel placementControls = null!;
    private readonly Dictionary<Guid,System.Windows.Shapes.Path> hierarchyPaths = [];
    private ChartScene? freeDragOriginal, freeDragPreview;
    private Guid freeDragId;
    private Point freeDragTarget;
    private bool freeDragQueued;

    private void SetFreeMove(bool enabled)
    {
        if (refreshing || Current is not Diagram chart || chart.FreeMove == enabled) return;
        Modify(c => { if (enabled) FreePlacement.Enable(c,WindowsNodeMetrics.Measure); else c.FreeMove = false; },false);
    }
    private void QueueFreeDrag(Guid id, double x, double y)
    {
        if (freeDragOriginal is null)
        {
            freeDragOriginal = documentScene; freeDragId = id; freeDragPreview = documentScene;
            CompositionTarget.Rendering += FreeDragFrame;
        }
        freeDragTarget = new(x,y); freeDragQueued = true;
    }
    private void FreeDragFrame(object? sender, EventArgs e)
    {
        if (!freeDragQueued || freeDragOriginal is null || Current is not Diagram chart) return;
        freeDragQueued = false;
        try
        {
            freeDragPreview = FreePlacement.Move(freeDragOriginal,freeDragId,freeDragTarget.X,freeDragTarget.Y);
            var bounds = OpenWorkspace.Bounds(chart,freeDragPreview);
            MakeWorldPointVisible(bounds.X,bounds.Y,bounds.Width,bounds.Height);
            documentScene = freeDragPreview; documentBounds = bounds;
            scene = OpenWorkspace.ToCanvas(freeDragPreview,canvasOrigin.X,canvasOrigin.Y);
            foreach (var box in scene.Boxes) if (nodeViews.TryGetValue(box.Id,out var view)) { Canvas.SetLeft(view,box.X); Canvas.SetTop(view,box.Y); }
            var boxes = scene.Boxes.ToDictionary(b => b.Id);
            foreach (var node in chart.Nodes.Where(n => n.ParentId.HasValue))
                if (hierarchyPaths.TryGetValue(node.Id,out var path) && boxes.TryGetValue(node.Id,out var child) && boxes.TryGetValue(node.ParentId!.Value,out var parent))
                { Canvas.SetLeft(path,0); Canvas.SetTop(path,0); path.Data = HierarchyGeometry(parent,child,chart.Layout,true); }
            canvas.Width = Math.Max(canvas.Width,scene.Width); canvas.Height = Math.Max(canvas.Height,scene.Height); UpdateCanvasExtent();
            status.Text = "Free move · nearby nodes make room · release to save · Esc to cancel";
        }
        catch (ArgumentException ex) { status.Text = ex.Message; }
    }
    private void EndFreeDrag(bool cancel = false)
    {
        if (freeDragOriginal is null) return;
        if (!cancel) FreeDragFrame(this,EventArgs.Empty);
        CompositionTarget.Rendering -= FreeDragFrame;
        var preview = freeDragPreview;
        freeDragOriginal = freeDragPreview = null; freeDragQueued = false;
        if (cancel || preview is null) Render(); else Modify(c => FreePlacement.Commit(c,preview));
    }
    private static Geometry HierarchyGeometry(NodeBox from,NodeBox to,ChartLayout layout,bool free = false)
    {
        var geometry = new StreamGeometry();
        if (free)
        {
            var route = FreePlacement.Connection(from,to,layout);
            using (var context = geometry.Open()) { context.BeginFigure(new(route.X1,route.Y1),false,false); if (layout is ChartLayout.MindMap or ChartLayout.RightTree) context.BezierTo(new(route.C1X,route.C1Y),new(route.C2X,route.C2Y),new(route.X2,route.Y2),true,false); else foreach(var point in LeadGeometry.Vertices(route).Skip(1)) context.LineTo(new(point.X,point.Y),true,false); }
            geometry.Freeze(); return geometry;
        }
        var c = ChartGeometry.Connector(from,to,layout);
        using (var context = geometry.Open())
        {
            context.BeginFigure(new(c.X1,c.Y1),false,false);
            if (layout is ChartLayout.MindMap or ChartLayout.RightTree) context.BezierTo(new((c.X1+c.X2)/2,c.Y1),new((c.X1+c.X2)/2,c.Y2),new(c.X2,c.Y2),true,false);
            else if (layout == ChartLayout.TopDown) { context.LineTo(new(c.X1,(c.Y1+c.Y2)/2),true,false); context.LineTo(new(c.X2,(c.Y1+c.Y2)/2),true,false); context.LineTo(new(c.X2,c.Y2),true,false); }
            else { context.LineTo(new((c.X1+c.X2)/2,c.Y1),true,false); context.LineTo(new((c.X1+c.X2)/2,c.Y2),true,false); context.LineTo(new(c.X2,c.Y2),true,false); }
        }
        geometry.Freeze(); return geometry;
    }
}
