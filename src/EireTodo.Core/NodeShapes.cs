namespace EireTodo.Core;

// Appended, independently saved shape choices; legacy styles keep their geometry.
public enum NodeShape { Design, Rectangle, RoundedRectangle, Square, Diamond, Circle, Ellipse, Trapezoid, Cloud, Hexagon, Triangle, Parallelogram }
public sealed record ShapeSegment(LeadPoint End, LeadPoint? Control1=null, LeadPoint? Control2=null);
public sealed record ShapeContour(LeadPoint Start,List<ShapeSegment> Segments);
public sealed record ShapeContent(double X,double Y,double Width,double Height);
public static class NodeShapes
{
    public static NodeShape Resolve(Diagram chart,ChartNode node)=>node.Shape??chart.Shape;
    public static string Name(NodeShape shape)=>shape switch { NodeShape.Design=>"Use design",NodeShape.RoundedRectangle=>"Rounded rectangle",_=>shape.ToString() };
    private static (double X,double Y) Insets(NodeShape shape)=>shape switch {
        NodeShape.Diamond=>(.25,.25),NodeShape.Circle or NodeShape.Ellipse=>(.15,.15),
        NodeShape.Trapezoid or NodeShape.Parallelogram=>(.22,.1),NodeShape.Cloud=>(.17,.22),
        NodeShape.Hexagon=>(.2,.2),NodeShape.Triangle=>(.28,.48),_=>(0,0) };
    public static (double Width,double Height) Expand((double Width,double Height) size,NodeShape shape)
    {
        var (x,y)=Insets(shape);var width=Math.Ceiling(size.Width/(1-2*x));var height=Math.Ceiling(size.Height/(shape==NodeShape.Triangle?.42:1-2*y));
        if(shape is NodeShape.Circle or NodeShape.Square)width=height=Math.Max(width,height);
        return(width,height);
    }
    public static ShapeContent Content(NodeShape shape,double width,double height)
    {
        var(x,y)=Insets(shape);return new(width*x,height*y,width*(1-2*x),height*(shape==NodeShape.Triangle?.42:1-2*y));
    }
    public static ShapeContour Contour(NodeShape shape,double width,double height,double x=0,double y=0)
    {
        LeadPoint P(double a,double b)=>new(x+Math.Clamp(a,0,1)*width,y+Math.Clamp(b,0,1)*height);
        ShapeContour Polygon(params (double X,double Y)[] points)=>new(P(points[0].X,points[0].Y),points.Skip(1).Select(p=>new ShapeSegment(P(p.X,p.Y))).ToList());
        switch(shape)
        {
            case NodeShape.Diamond:return Polygon((.5,0),(1,.5),(.5,1),(0,.5));
            case NodeShape.Trapezoid:return Polygon((.2,0),(.8,0),(1,1),(0,1));
            case NodeShape.Hexagon:return Polygon((.25,0),(.75,0),(1,.5),(.75,1),(.25,1),(0,.5));
            case NodeShape.Triangle:return Polygon((.5,0),(1,1),(0,1));
            case NodeShape.Parallelogram:return Polygon((.2,0),(1,0),(.8,1),(0,1));
            case NodeShape.Circle:case NodeShape.Ellipse:
                const double k=.2761423749;
                return new(P(.5,0),[new(P(1,.5),P(.5+k,0),P(1,.5-k)),new(P(.5,1),P(1,.5+k),P(.5+k,1)),new(P(0,.5),P(.5-k,1),P(0,.5+k)),new(P(.5,0),P(0,.5-k),P(.5-k,0))]);
            case NodeShape.Cloud:
                return new(P(.16,.82),[new(P(.06,.42),P(-.05,.82),P(-.01,.45)),new(P(.31,.12),P(.01,.16),P(.17,.05)),new(P(.66,.10),P(.34,-.03),P(.58,-.04)),new(P(.90,.28),P(.83,.02),P(.96,.13)),new(P(.92,.70),P(1.06,.29),P(1.06,.60)),new(P(.72,.88),P(1.01,.85),P(.87,.99)),new(P(.40,.94),P(.68,1.02),P(.48,1.03)),new(P(.16,.82),P(.28,1.04),P(.12,.98))]);
            default:return Polygon((0,0),(1,0),(1,1),(0,1));
        }
    }
    public static LeadPoint Port(NodeBox box,LeadSide side)
    {
        var fallback=side switch {LeadSide.Top=>new LeadPoint(box.X+box.Width/2,box.Y),LeadSide.Bottom=>new(box.X+box.Width/2,box.Y+box.Height),LeadSide.Left=>new(box.X,box.Y+box.Height/2),_=>new(box.X+box.Width,box.Y+box.Height/2)};
        if(box.Shape is NodeShape.Design or NodeShape.Rectangle or NodeShape.Square or NodeShape.RoundedRectangle)return fallback;
        var contour=Contour(box.Shape,box.Width,box.Height,box.X,box.Y);var points=new List<LeadPoint>{contour.Start};var start=contour.Start;
        foreach(var segment in contour.Segments)
        {
            if(segment.Control1 is {} a && segment.Control2 is {} b)for(var i=1;i<=24;i++){var t=i/24d;var u=1-t;points.Add(new(u*u*u*start.X+3*u*u*t*a.X+3*u*t*t*b.X+t*t*t*segment.End.X,u*u*u*start.Y+3*u*u*t*a.Y+3*u*t*t*b.Y+t*t*t*segment.End.Y));}
            else points.Add(segment.End);start=segment.End;
        }
        points.Add(contour.Start);var vertical=side is LeadSide.Top or LeadSide.Bottom;var target=vertical?box.X+box.Width/2:box.Y+box.Height/2;var hits=new List<LeadPoint>();
        for(var i=1;i<points.Count;i++){var a=points[i-1];var b=points[i];var lo=vertical?a.X:a.Y;var hi=vertical?b.X:b.Y;if(Math.Abs(hi-lo)<1e-9||target<Math.Min(lo,hi)||target>Math.Max(lo,hi))continue;var t=(target-lo)/(hi-lo);hits.Add(new(a.X+t*(b.X-a.X),a.Y+t*(b.Y-a.Y)));}
        return hits.Count==0?fallback:side switch{LeadSide.Top=>hits.MinBy(p=>p.Y)!,LeadSide.Bottom=>hits.MaxBy(p=>p.Y)!,LeadSide.Left=>hits.MinBy(p=>p.X)!,_=>hits.MaxBy(p=>p.X)!};
    }
    public static void MakeRoom(Diagram chart,Guid? pinned=null,Func<ChartNode,int,(double Width,double Height)>? measure=null)
    {
        if(chart.Kind!=DiagramKind.Network||chart.Nodes.Count==0)return;
        var scene=ChartGeometry.Arrange(chart,measure:measure);var anchor=scene.Boxes.FirstOrDefault(b=>b.Id==pinned)??scene.Boxes[0];var settled=FreePlacement.Move(scene,anchor.Id,anchor.X,anchor.Y);var positions=settled.Boxes.ToDictionary(b=>b.Id);
        foreach(var node in chart.Nodes){node.X=positions[node.Id].X;node.Y=positions[node.Id].Y;}
    }
    public static void Set(Diagram chart,IEnumerable<Guid> ids,NodeShape? shape)
    {
        if(shape.HasValue&&!Enum.IsDefined(shape.Value))throw new ArgumentException("Choose a valid node shape.");var selected=ids.ToHashSet();if(selected.Count==0||selected.Any(id=>!chart.Nodes.Any(n=>n.Id==id)))throw new ArgumentException("Select existing nodes.");
        foreach(var node in chart.Nodes.Where(n=>selected.Contains(n.Id)))node.Shape=shape;
    }
}
