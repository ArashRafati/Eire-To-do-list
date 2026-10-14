using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using EireTodo.Core;
namespace EireTodo.Windows;

internal static class NodeShapeDrawing
{
    internal static Geometry Geometry(NodeShape shape,double width,double height,double x=0,double y=0)
    {
        if(shape==NodeShape.RoundedRectangle)return new RectangleGeometry(new Rect(x,y,width,height),10,10);
        var contour=NodeShapes.Contour(shape,width,height,x,y);var result=new StreamGeometry();using(var g=result.Open())
        {g.BeginFigure(new(contour.Start.X,contour.Start.Y),true,true);foreach(var segment in contour.Segments){if(segment.Control1 is {} c1&&segment.Control2 is {} c2)g.BezierTo(new(c1.X,c1.Y),new(c2.X,c2.Y),new(segment.End.X,segment.End.Y),true,false);else g.LineTo(new(segment.End.X,segment.End.Y),true,false);}}result.Freeze();return result;
    }
}
internal sealed partial class ChartWorkspace
{
    private void ChooseShape(bool wholeDiagram)
    {
        if(!FinishInlineEdit()||Current is not Diagram chart)return;
        var menu=new ContextMenu();
        foreach(var shape in Enum.GetValues<NodeShape>())
        {
            var item=new MenuItem {Header=NodeShapes.Name(shape),IsCheckable=true,IsChecked=wholeDiagram?chart.Shape==shape:selectedId is Guid id&&chart.Nodes.Single(n=>n.Id==id).Shape==shape};
            item.Icon=new Path {Data=NodeShapeDrawing.Geometry(shape,shape is NodeShape.Circle or NodeShape.Square?16:22,16),Fill=Ui.Brush("SoftTeal"),Stroke=Ui.Accent,StrokeThickness=1,Width=22,Height=16};
            item.Click+=(_,_)=>Modify(c=>{if(wholeDiagram){c.Shape=shape;foreach(var node in c.Nodes)node.Shape=null;}else NodeShapes.Set(c,selectedNodes,shape);NodeShapes.MakeRoom(c,selectedId,WindowsNodeMetrics.Measure);},false);menu.Items.Add(item);
        }
        if(!wholeDiagram){menu.Items.Add(new Separator());menu.Items.Add(WorkspaceMenus.Action("Use diagram shape",()=>Modify(c=>{NodeShapes.Set(c,selectedNodes,null);NodeShapes.MakeRoom(c,selectedId,WindowsNodeMetrics.Measure);},false)));}
        menu.PlacementTarget=viewport;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.MousePoint;menu.IsOpen=true;
    }
    private void UnlinkNodes()
    {
        if(!FinishInlineEdit()||Current is not Diagram chart)return;var count=0;
        if(!Try(()=>{var candidate=chart.Clone();count=GraphConnections.Unlink(candidate,selectedNodes);Commit(candidate);}))return;
        selectedLeadId=null;RefreshData();status.Text=$"Removed {count} links · Nodes retained · Ctrl+Z restores links.";
    }
}
