using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using EireTodo.Core;
using PdfSharp.Pdf.IO;

internal static class InteractionChecks
{
    private static void Assert(bool condition) { if(!condition)throw new Exception("Interaction assertion failed."); }
    internal static void Run(Action<string,Action> check,string directory)
    {
        check("Enter confirms an active title before the next distinct press creates a node",()=>
        {
            Assert(NodeInputPolicy.Enter(true)==NodeEnterAction.ConfirmTitle);Assert(NodeInputPolicy.Enter(false)==NodeEnterAction.CreateNode);
            Assert(NodeInputPolicy.Enter(true,true)==NodeEnterAction.Ignore&&NodeInputPolicy.Enter(false,true)==NodeEnterAction.Ignore);
        });
        check("Moving 1.6.1 between 1.5 and 1.6 promotes the branch and updates WBS sequence",()=>
        {
            var chart=Charts.Create("Root",ChartLayout.TopDown);var root=chart.Nodes[0];for(var i=0;i<7;i++)Charts.Add(chart,root.Id,true,"Branch "+(i+1));
            var branches=Charts.Children(chart,root.Id);var moved=Charts.Add(chart,branches[5].Id,true,"Chain supports");var child=Charts.Add(chart,moved.Id,true,"Preserved child");var activity=moved.ActivityId;
            Assert(Charts.Outline(chart).Single(o=>o.Node.Id==moved.Id).Code=="1.6.1");
            var slots=HierarchyDropSlots.Create(chart,ChartGeometry.Arrange(chart));var before=slots.Single(s=>s.Target==branches[5].Id&&s.Drop==NodeDrop.Before);Assert(before.Width==ChartGeometry.Gap);
            Charts.MoveRelative(chart,moved.Id,before.Target,before.Drop);var outline=Charts.Outline(chart);
            Assert(outline.Single(o=>o.Node.Id==branches[4].Id).Code=="1.5");Assert(outline.Single(o=>o.Node.Id==moved.Id).Code=="1.6");Assert(outline.Single(o=>o.Node.Id==branches[5].Id).Code=="1.7");Assert(outline.Single(o=>o.Node.Id==child.Id).Code=="1.6.1"&&moved.ActivityId==activity);
            foreach(var layout in Enum.GetValues<ChartLayout>().Where(l=>l!=ChartLayout.Freeform)) { chart.Layout=layout;Assert(HierarchyDropSlots.Create(chart,ChartGeometry.Arrange(chart)).Any(s=>s.Target==moved.Id)); }
        });
        check("Compact tiered nodes reserve enough height for long titles and remain collision-free",()=>
        {
            var chart=Charts.Create("Short root",ChartLayout.TopDown);var branch=Charts.Add(chart,chart.Nodes[0].Id,true,"Branch");var leaf=Charts.Add(chart,branch.Id,true,"Short leaf");
            var longNode=Charts.Add(chart,branch.Id,true,"DN100 Stub Pipe vent from Drain Valve - stainless steel fabrication and supports, café Ω");
            var scene=ChartGeometry.Arrange(chart);var boxes=scene.Boxes.ToDictionary(b=>b.Id);
            Assert(boxes[chart.Nodes[0].Id].Width>boxes[branch.Id].Width&&boxes[branch.Id].Width>boxes[leaf.Id].Width);Assert(boxes[leaf.Id].Height<100&&boxes[longNode.Id].Height>boxes[leaf.Id].Height);
            foreach(var layout in Enum.GetValues<ChartLayout>().Where(l=>l!=ChartLayout.Freeform)) { chart.Layout=layout;scene=ChartGeometry.Arrange(chart);for(var i=0;i<scene.Boxes.Count;i++)for(var j=i+1;j<scene.Boxes.Count;j++) { var a=scene.Boxes[i];var b=scene.Boxes[j];Assert(a.X+a.Width<=b.X+.01||b.X+b.Width<=a.X+.01||a.Y+a.Height<=b.Y+.01||b.Y+b.Height<=a.Y+.01); } }
            var drawing=DiagramPdf.Compose(chart,new PdfOptions(IncludeDetails:false));var all=string.Concat(drawing.Pages[0].Layers.SelectMany(l=>l.Marks).OfType<PdfText>().Select(t=>t.Text));Assert(all.Contains(longNode.Title));
        });
        check("Both lead ends attach to exact side centres with orthogonal sharp bends and upright labels",()=>
        {
            var a=new NodeBox(Guid.NewGuid(),-200,-100,220,80);var b=new NodeBox(Guid.NewGuid(),450,320,180,66);
            foreach(var from in Enum.GetValues<LeadSide>().Where(s=>s!=LeadSide.Auto))foreach(var to in Enum.GetValues<LeadSide>().Where(s=>s!=LeadSide.Auto))foreach(var routing in Enum.GetValues<LeadRouting>())
            {
                var path=LeadGeometry.Route(a,b,routing,fromSide:from,toSide:to);var start=LeadGeometry.Port(a,from);var end=LeadGeometry.Port(b,to);
                Assert(path.X1==start.X&&path.Y1==start.Y&&path.X2==end.X&&path.Y2==end.Y);Assert(path.LabelAngle>=-90&&path.LabelAngle<=90);
                if(routing==LeadRouting.SharpBends)foreach(var pair in LeadGeometry.Vertices(path).Zip(LeadGeometry.Vertices(path).Skip(1)))Assert(Math.Abs(pair.First.X-pair.Second.X)<.001||Math.Abs(pair.First.Y-pair.Second.Y)<.001);
            }
        });
        check("Lead geometry stays stable when the viewport origin changes, including obstacle routing",()=>
        {
            var a=new NodeBox(Guid.NewGuid(),-800,-400,220,90);var b=new NodeBox(Guid.NewGuid(),300,100,180,80);var obstacle=new NodeBox(Guid.NewGuid(),-350,-190,220,200);
            foreach(var routing in Enum.GetValues<LeadRouting>()) foreach(var side in Enum.GetValues<LeadSide>())
            {
                var before=LeadGeometry.Route(a,b,routing,obstacles:[a,b,obstacle],fromSide:side,toSide:side);
                NodeBox Shift(NodeBox box)=>box with { X=box.X+4096,Y=box.Y+4096 };
                var after=LeadGeometry.Route(Shift(a),Shift(b),routing,obstacles:[Shift(a),Shift(b),Shift(obstacle)],fromSide:side,toSide:side);
                Assert(LeadGeometry.Vertices(before).Zip(LeadGeometry.Vertices(after)).All(p=>Math.Abs(p.Second.X-p.First.X-4096)<.01&&Math.Abs(p.Second.Y-p.First.Y-4096)<.01));
                Assert(Math.Abs(after.LabelX-before.LabelX-4096)<.01&&Math.Abs(after.LabelY-before.LabelY-4096)<.01);
            }
        });
        check("Lead labels, side attachments, palettes and designs persist and export without altering IDs",()=>
        {
            var chart=NetworkCharts.Create("Styled graph");var node=GraphCreation.Add(chart,chart.Nodes[0].Id,"Next");var lead=chart.Leads.Single();lead.FromSide=LeadSide.Bottom;lead.ToSide=LeadSide.Left;lead.Description="Fabricate → inspect café Ω";chart.Palette=DiagramPalette.Ocean;chart.Design=NodeDesign.Rounded;
            var store=new DataStore(Path.Combine(directory,"ports-and-palettes"));var service=new TodoService(store,store.Load());service.SaveDiagram(chart);var backup=Path.Combine(directory,"ports-backup.json");service.Export(backup);service.DeleteDiagram(chart.Id);service.Restore(backup);var loaded=store.Load().Diagrams.Single();
            Assert(loaded.Palette==DiagramPalette.Ocean&&loaded.Design==NodeDesign.Rounded&&loaded.Leads.Single().FromSide==LeadSide.Bottom&&loaded.Leads.Single().ToSide==LeadSide.Left&&loaded.Leads.Single().Id==lead.Id);
            var csv=Encoding.UTF8.GetString(NetworkExports.Export(loaded,ChartExportFormat.NetworkCsv));Assert(csv.Contains("From Side")&&csv.Contains("Bottom")&&csv.Contains("Fabricate → inspect café Ω"));
            using var xmlStream=new MemoryStream(NetworkExports.Export(loaded,ChartExportFormat.NetworkXml));var xml=XDocument.Load(xmlStream);XNamespace ns=NetworkExports.XmlNamespace;Assert((string?)xml.Root!.Attribute("palette")=="Ocean"&&(string?)xml.Root.Element(ns+"Leads")!.Elements().Single().Attribute("toSide")=="Left");
            loaded.Leads.Clear();service.SaveDiagram(loaded);Assert(store.Load().Diagrams.Single().Nodes.Any(n=>n.Id==node.Id)&&store.Load().Diagrams.Single().Leads.Count==0);
        });
        check("PDF defaults to a fitted A4 landscape sheet and supports portrait, A3, tiles and detail pages",()=>
        {
            var chart=Charts.Create("Paper sizing",ChartLayout.TopDown);for(var i=0;i<22;i++)Charts.Add(chart,chart.Nodes[0].Id,true,"Sibling "+i);
            foreach(var paper in Enum.GetValues<PdfPaper>())foreach(var orientation in Enum.GetValues<PdfOrientation>())
            {
                var drawing=DiagramPdf.Compose(chart,new(paper,orientation,IncludeDetails:false));Assert(drawing.Pages.Count==1);var page=drawing.Pages[0];Assert(orientation==PdfOrientation.Landscape?page.Width>page.Height:page.Height>page.Width);
                var scene=ChartGeometry.Arrange(chart,true);var layer=page.Layers[1];var clip=layer.Clip!;Assert(layer.X>=clip.X&&layer.Y>=clip.Y&&layer.X+scene.Width*layer.Scale<=clip.X+clip.Width+.01&&layer.Y+scene.Height*layer.Scale<=clip.Y+clip.Height+.01);
                using var stream=new MemoryStream(DiagramPdf.Write(drawing));using var pdf=PdfReader.Open(stream,PdfDocumentOpenMode.Import);Assert(pdf.PageCount==drawing.Pages.Count&&Math.Abs(pdf.Pages[0].Width.Point-page.Width)<.01&&Math.Abs(pdf.Pages[0].Height.Point-page.Height)<.01);
            }
            var tiled=DiagramPdf.Compose(chart,new(FitToPaper:false,IncludeDetails:false));Assert(tiled.Pages.Count>1);Assert(DiagramPdf.Compose(chart).Pages.Count>1);
        });
    }
    internal static void WriteFixtures(string directory)
    {
        var chart=UpdateChecks.GraphFixture();chart.Palette=DiagramPalette.Ocean;chart.Design=NodeDesign.Rounded;chart.Leads[0].FromSide=LeadSide.Bottom;chart.Leads[0].ToSide=LeadSide.Top;chart.Leads[1].FromSide=LeadSide.Right;chart.Leads[1].ToSide=LeadSide.Left;
        File.WriteAllBytes(Path.Combine(directory,"preview-landscape.pdf"),ChartExports.Pdf(chart,new(IncludeDetails:false)));
        File.WriteAllBytes(Path.Combine(directory,"preview-portrait.pdf"),ChartExports.Pdf(chart,new(Orientation:PdfOrientation.Portrait,IncludeDetails:false)));
        File.WriteAllBytes(Path.Combine(directory,"preview-details.pdf"),ChartExports.Pdf(chart));
    }
}
