using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace EireTodo.Core;

public enum PdfPaper { A4, A3 }
public enum PdfOrientation { Landscape, Portrait }
public sealed record PdfOptions(PdfPaper Paper = PdfPaper.A4, PdfOrientation Orientation = PdfOrientation.Landscape, bool FitToPaper = true, bool IncludeDetails = true);
public abstract record PdfMark;
public sealed record PdfText(double X, double Y, string Text, double Size, string Colour = "#000000", bool Bold = false, bool Italic = false, bool Underline = false, double Angle = 0, bool Centre = false, string Backdrop = "") : PdfMark;
public sealed record PdfImage(double X, double Y, double Width, double Height, byte[] Png) : PdfMark;
public sealed record PdfNodeShape(double X,double Y,double Width,double Height,NodeShape Shape,string Fill,string Stroke,double Thickness=1) : PdfMark { public ShapeContour Contour=>NodeShapes.Contour(Shape,Width,Height,X,Y); }
public sealed record PdfBox(double X, double Y, double Width, double Height, string Fill, string Stroke = "", double Radius = 0, double Thickness = 1) : PdfMark;
public sealed record PdfLine(IReadOnlyList<LeadPoint> Points, string Colour, double Thickness = 1.5, bool Curve = false, bool Closed = false, string Fill = "") : PdfMark;
public sealed record PdfClip(double X, double Y, double Width, double Height);
public sealed class PdfLayer
{
    public double Scale { get; init; } = 1;
    public double X { get; init; }
    public double Y { get; init; }
    public PdfClip? Clip { get; init; }
    public List<PdfMark> Marks { get; } = [];
}
public sealed record PdfSheet(double Width, double Height, List<PdfLayer> Layers);
public sealed record PdfDrawing(string Title, List<PdfSheet> Pages);

// Both the WPF paper preview and the PDF writer render this same vector drawing.
public static class DiagramPdf
{
    public static PdfDrawing Compose(Diagram diagram, PdfOptions? options = null)
    {
        Charts.Validate(diagram); options ??= new();
        if (!Enum.IsDefined(options.Paper) || !Enum.IsDefined(options.Orientation)) throw new ArgumentException("Choose a valid paper size and orientation.");
        ChartExports.EnsureFonts();
        using var measureDocument = new PdfDocument(); var measurePage = measureDocument.AddPage(); using var graphics = XGraphics.FromPdfPage(measurePage);
        var shortSide = options.Paper == PdfPaper.A4 ? 595.276 : 841.89; var longSide = options.Paper == PdfPaper.A4 ? 841.89 : 1190.551;
        var width = options.Orientation == PdfOrientation.Landscape ? longSide : shortSide; var height = options.Orientation == PdfOrientation.Landscape ? shortSide : longSide;
        const double margin = 28;
        var logo = OrganisationBranding.Fit(diagram.OrganisationLogoPng,diagram.ExportLogoWidth,120);
        var titleX = margin + (logo.Width > 0 ? logo.Width + 14 : 0);
        var titleLines = ChartExports.Wrap(graphics,diagram.Name,Font(18),width-margin-titleX).ToList();
        var top = logo.Width > 0 ? Math.Max(logo.Height+margin+16,46+titleLines.Count*22+24) : 64; var areaWidth = width-2*margin; var areaHeight = height-top-margin;
        var scene = diagram.Kind == DiagramKind.Network || diagram.FreeMove ? OpenWorkspace.CompactGraph(diagram,true) : ChartGeometry.Arrange(diagram,true);
        var scale = options.FitToPaper ? Math.Min(1,Math.Min(areaWidth/Math.Max(1,scene.Width),areaHeight/Math.Max(1,scene.Height))) : .75;
        var tileWidth = areaWidth/scale; var tileHeight = areaHeight/scale;
        var columns = options.FitToPaper ? 1 : Math.Max(1,(int)Math.Ceiling(scene.Width/tileWidth)); var rows = options.FitToPaper ? 1 : Math.Max(1,(int)Math.Ceiling(scene.Height/tileHeight));
        var boxes = scene.Boxes.ToDictionary(b=>b.Id); var outline = Charts.Outline(diagram).ToDictionary(o=>o.Node.Id); var levels = DiagramAppearance.Levels(diagram); var colours = DiagramAppearance.Colours(diagram.Palette);
        var chartMarks = new List<PdfMark>();
        foreach (var box in scene.Boxes)
        {
            var node = outline[box.Id].Node;
            if (node.ParentId is Guid parent && boxes.TryGetValue(parent,out var from))
            {
                if (diagram.FreeMove)
                {
                    var route = FreePlacement.Connection(from,box,diagram.Layout);
                    chartMarks.Add(new PdfLine(LeadGeometry.Vertices(route),colours.Lead,1.5,diagram.Layout is ChartLayout.MindMap or ChartLayout.RightTree or ChartLayout.LeftTree)); continue;
                }
                var c = ChartGeometry.Connector(from,box,diagram.Layout);
                if (diagram.Layout is ChartLayout.MindMap or ChartLayout.RightTree or ChartLayout.LeftTree) chartMarks.Add(new PdfLine([new(c.X1,c.Y1),new((c.X1+c.X2)/2,c.Y1),new((c.X1+c.X2)/2,c.Y2),new(c.X2,c.Y2)],colours.Lead,1.5,true));
                else if (ChartGeometry.IsVertical(diagram.Layout)) chartMarks.Add(new PdfLine([new(c.X1,c.Y1),new(c.X1,(c.Y1+c.Y2)/2),new(c.X2,(c.Y1+c.Y2)/2),new(c.X2,c.Y2)],colours.Lead));
                else chartMarks.Add(new PdfLine([new(c.X1,c.Y1),new((c.X1+c.X2)/2,c.Y1),new((c.X1+c.X2)/2,c.Y2),new(c.X2,c.Y2)],colours.Lead));
            }
        }
        foreach (var group in diagram.Leads.GroupBy(l=>l.From.CompareTo(l.To)<0?(l.From,l.To):(l.To,l.From)))
        {
            var leads = group.ToList();
            for (var i=0;i<leads.Count;i++)
            {
                var lead=leads[i]; var p=LeadGeometry.Route(boxes[lead.From],boxes[lead.To],lead.Routing,i-leads.Count/2,scene.Boxes,lead.FromSide,lead.ToSide);
                chartMarks.Add(new PdfLine(LeadGeometry.Vertices(p),colours.Lead,2,lead.Routing==LeadRouting.Curve)); Arrow(p.X2,p.Y2,p.C2X,p.C2Y); if (lead.DoubleHeaded) Arrow(p.X1,p.Y1,p.C1X,p.C1Y);
                if (!string.IsNullOrWhiteSpace(lead.Description))
                {
                    var font=Font(12); var label=lead.Description.Replace("\r", "").Replace("\n", " "); var original=label;
                    while(label.Length>1&&graphics.MeasureString(label+"…",font).Width>p.LabelWidth) label=label[..^1]; if(label.Length<original.Length)label+="…";
                    // The paper preview and exported file both use the same upright tangent and baseline.
                    chartMarks.Add(new PdfText(p.LabelX,p.LabelY,label,12,BrandTheme.Ink,Angle:p.LabelAngle,Centre:true,Backdrop:BrandTheme.White));
                }
            }
        }
        foreach(var originalBox in scene.Boxes)
        {
            var box=originalBox;
            var o=outline[box.Id]; var node=o.Node; var due=Overdue.IsDue(node.FinishDate,node.Completed); var level=levels[node.Id];
            var surface = DiagramAppearance.Surface(diagram,node,level);
            var shape=NodeShapes.Resolve(diagram,node);
            if(shape!=NodeShape.Design)chartMarks.Add(new PdfNodeShape(box.X,box.Y,box.Width,box.Height,shape,surface.Fill,surface.Stroke,surface.Thickness));
            else if(surface.Decoration is NodeDecoration.Box or NodeDecoration.AccentBar or NodeDecoration.TopBar) chartMarks.Add(new PdfBox(box.X,box.Y,box.Width,box.Height,surface.Fill,surface.Decoration == NodeDecoration.Box ? surface.Stroke : "",Math.Min(surface.Radius, Math.Min(box.Width,box.Height)/2),surface.Thickness));
            if(surface.Decoration == NodeDecoration.Underline) chartMarks.Add(new PdfLine([new(box.X,box.Y+box.Height-1),new(box.X+box.Width,box.Y+box.Height-1)],surface.Stroke,2));
            if(surface.Decoration == NodeDecoration.AccentBar) chartMarks.Add(new PdfLine([new(box.X+2,box.Y),new(box.X+2,box.Y+box.Height)],surface.Stroke,4));
            if(surface.Decoration == NodeDecoration.TopBar) chartMarks.Add(new PdfLine([new(box.X,box.Y+2),new(box.X+box.Width,box.Y+2)],surface.Stroke,4));
            var content=NodeShapes.Content(shape,box.Width,box.Height);box=new NodeBox(box.Id,box.X+content.X,box.Y+content.Y,content.Width,content.Height);
            chartMarks.Add(new PdfText(box.X+11,box.Y+19,o.Code,12,DiagramAppearance.Text(diagram,node,level)));
            var format=node.Format; var font=Font(format.Size,format.Bold,format.Italic,format.Underline); var lines=ChartExports.Wrap(graphics,node.Title,font,box.Width-28).ToList();
            for(var i=0;i<lines.Count;i++)
            {
                var length=graphics.MeasureString(lines[i],font).Width;
                var x=format.Alignment==TextJustification.Centre?box.X+(box.Width-length)/2:format.Alignment==TextJustification.Right?box.X+box.Width-12-length:box.X+12;
                chartMarks.Add(new PdfText(x,box.Y+29+format.Size+i*format.Size*1.35,lines[i],format.Size,DiagramAppearance.Text(diagram,node,level),format.Bold,format.Italic,format.Underline));
            }
            if (node.ShowNotes && !string.IsNullOrWhiteSpace(node.Notes))
            {
                var notes = ChartExports.Wrap(graphics,node.Notes,Font(12),box.Width-28).ToList();
                var y = box.Y + 38 + lines.Count * format.Size * 1.35;
                for (var i = 0; i < notes.Count; i++) chartMarks.Add(new PdfText(box.X+12,y+i*16.2,notes[i],12,surface.Text));
            }
        }
        var pages=new List<PdfSheet>();
        for(var row=0;row<rows;row++)for(var col=0;col<columns;col++)
        {
            var header=Header(); header.Marks.Add(new PdfText(titleX,logo.Width>0?46+titleLines.Count*22:48,$"{Charts.LayoutName(diagram.Layout)} · {(options.FitToPaper?"Fit to paper":$"Tile {row+1}/{rows}, {col+1}/{columns}")} · All nodes",10));
            var layer=new PdfLayer { Scale=scale, X=margin-col*tileWidth*scale+(options.FitToPaper?(areaWidth-scene.Width*scale)/2:0), Y=top-row*tileHeight*scale+(options.FitToPaper?(areaHeight-scene.Height*scale)/2:0), Clip=new(margin,top,areaWidth,areaHeight) }; layer.Marks.AddRange(chartMarks);
            var footer=new PdfLayer(); footer.Marks.Add(new PdfText(margin,height-12,$"SUMAPP · Page {pages.Count+1}",10)); pages.Add(new(width,height,[header,layer,footer]));
        }
        if(options.IncludeDetails)
        {
            PdfLayer? detail=null; double y=0;
            void NewPage() { detail=Header(); detail.Marks.Add(new PdfText(titleX,logo.Width>0?46+titleLines.Count*22:48,"Full details",10)); pages.Add(new(width,height,[detail])); y=top; }
            void Paragraph(string text)
            {
                foreach(var line in ChartExports.Wrap(graphics,text,Font(12),areaWidth)) { if(detail is null||y>height-margin)NewPage(); detail!.Marks.Add(new PdfText(margin,y,line,12)); y+=17; } y+=12;
            }
            foreach(var o in Charts.Outline(diagram)) { var n=o.Node; Paragraph($"{o.Code}  {n.Title}\nID {n.ActivityId} | Start {AustralianDates.Format(n.StartDate)} | Finish {AustralianDates.Format(n.FinishDate)} | Duration {n.DurationDays} days | {(n.Completed?"Completed":"To do")}\n{n.Notes}"); }
            foreach(var lead in diagram.Leads)Paragraph($"Lead: {diagram.Nodes.Single(n=>n.Id==lead.From).Title} {(lead.DoubleHeaded?"↔":"→")} {diagram.Nodes.Single(n=>n.Id==lead.To).Title} · {lead.Routing} · {lead.FromSide} / {lead.ToSide}\n{lead.Description}");
        }
        return new(diagram.Name,pages);
        PdfLayer Header()
        {
            var layer=new PdfLayer();
            if(logo.Width>0) layer.Marks.Add(new PdfImage(margin,margin,logo.Width,logo.Height,diagram.OrganisationLogoPng));
            if(logo.Width>0) for(int i=0;i<titleLines.Count;i++) layer.Marks.Add(new PdfText(titleX,46+i*22,titleLines[i],18));
            else layer.Marks.Add(new PdfText(margin,29,diagram.Name,18));
            return layer;
        }
        void Arrow(double x,double y,double px,double py) { var angle=Math.Atan2(y-py,x-px)+Math.PI; chartMarks.Add(new PdfLine([new(x,y),new(x+11*Math.Cos(angle+.42),y+11*Math.Sin(angle+.42)),new(x+11*Math.Cos(angle-.42),y+11*Math.Sin(angle-.42))],colours.Accent,1,Closed:true,Fill:colours.Accent)); }
    }
    internal static XFont Font(double size,bool bold=false,bool italic=false,bool underline=false) => new("EireExport",size,(bold?XFontStyleEx.Bold:XFontStyleEx.Regular)|(italic?XFontStyleEx.Italic:XFontStyleEx.Regular)|(underline?XFontStyleEx.Underline:XFontStyleEx.Regular));
    private static XColor Colour(string hex) => XColor.FromArgb(Convert.ToInt32(hex.TrimStart('#'),16)|unchecked((int)0xFF000000));
    public static void Save(PdfDrawing drawing, string path) => DataStore.WriteExport(path,Write(drawing));
    public static void SaveBytes(byte[] bytes, string path) => DataStore.WriteExport(path,bytes);
    public static byte[] Write(PdfDrawing drawing)
    {
        ChartExports.EnsureFonts(); using var document=new PdfDocument(); document.Info.Title=drawing.Title; document.Info.Creator="SUMAPP · To-do / Mind map / WBS / Graph";
        foreach(var sheet in drawing.Pages)
        {
            var page=document.AddPage();page.Width=XUnit.FromPoint(sheet.Width);page.Height=XUnit.FromPoint(sheet.Height);using var g=XGraphics.FromPdfPage(page);
            foreach(var layer in sheet.Layers)
            {
                var state=g.Save(); if(layer.Clip is {} clip)g.IntersectClip(new XRect(clip.X,clip.Y,clip.Width,clip.Height));g.TranslateTransform(layer.X,layer.Y);g.ScaleTransform(layer.Scale);
                foreach(var mark in layer.Marks)switch(mark)
                {
                    case PdfImage image:
                        using (var logoStream=new MemoryStream(image.Png)) using (var picture=XImage.FromStream(logoStream)) g.DrawImage(picture,image.X,image.Y,image.Width,image.Height);
                        break;
                    case PdfText t:
                        var textState=g.Save();g.TranslateTransform(t.X,t.Y);g.RotateTransform(t.Angle);var font=Font(t.Size,t.Bold,t.Italic,t.Underline); var length=g.MeasureString(t.Text,font).Width; var x=t.Centre?-length/2:0; if(t.Backdrop.Length>0)g.DrawRectangle(new XSolidBrush(Colour(t.Backdrop)),x-4,-t.Size,length+8,t.Size*1.4);g.DrawString(t.Text,font,new XSolidBrush(Colour(t.Colour)),new XPoint(x,0));g.Restore(textState);break;
                    case PdfNodeShape nodeShape:
                        var contour=NodeShapes.Contour(nodeShape.Shape,nodeShape.Width,nodeShape.Height,nodeShape.X,nodeShape.Y);var shapePath=new XGraphicsPath();var previous=contour.Start;
                        foreach(var segment in contour.Segments){if(segment.Control1 is {} c1&&segment.Control2 is {} c2)shapePath.AddBezier(new(previous.X,previous.Y),new(c1.X,c1.Y),new(c2.X,c2.Y),new(segment.End.X,segment.End.Y));else shapePath.AddLine(previous.X,previous.Y,segment.End.X,segment.End.Y);previous=segment.End;}shapePath.CloseFigure();
                        if(nodeShape.Shape==NodeShape.RoundedRectangle)g.DrawRoundedRectangle(new XPen(Colour(nodeShape.Stroke),nodeShape.Thickness),new XSolidBrush(Colour(nodeShape.Fill)),new XRect(nodeShape.X,nodeShape.Y,nodeShape.Width,nodeShape.Height),new XSize(20,20));
                        else g.DrawPath(new XPen(Colour(nodeShape.Stroke),nodeShape.Thickness),new XSolidBrush(Colour(nodeShape.Fill)),shapePath);break;
                    case PdfBox b:
                        var box=new XRect(b.X,b.Y,b.Width,b.Height);var fill=new XSolidBrush(Colour(b.Fill));var pen=b.Stroke.Length>0?new XPen(Colour(b.Stroke),b.Thickness):null;
                        if(b.Radius>0)g.DrawRoundedRectangle(pen,fill,box,new XSize(b.Radius*2,b.Radius*2));else g.DrawRectangle(pen,fill,box);break;
                    case PdfLine l:
                        var path=new XGraphicsPath();var points=l.Points.Select(p=>new XPoint(p.X,p.Y)).ToArray();if(l.Curve)path.AddBezier(points[0],points[1],points[2],points[3]);else path.AddLines(points);if(l.Closed)path.CloseFigure();
                        if(l.Fill.Length>0)g.DrawPath(new XSolidBrush(Colour(l.Fill)),path);else g.DrawPath(new XPen(Colour(l.Colour),l.Thickness),path);break;
                }
                g.Restore(state);
            }
        }
        using var stream=new MemoryStream();document.Save(stream,false);return stream.ToArray();
    }
}
