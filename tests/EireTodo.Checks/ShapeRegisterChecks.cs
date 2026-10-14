using System.Text.Json;
using EireTodo.Core;
namespace EireTodo.Checks;
internal static class ShapeRegisterChecks
{
    private static void Assert(bool condition,string message="Shape/register check failed."){if(!condition)throw new Exception(message);}
    private static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid change was accepted.");}
    internal static void Run(Action<string,Action> check,string root)
    {
        check("Every explicit shape reserves readable content space including large titles and full notes",()=>
        {
            foreach(var shape in Enum.GetValues<NodeShape>())foreach(var size in new[]{12d,17,48})
            {
                var node=new ChartNode {Title="Engineering review — full wrapped title for stainless steel fabrication",Notes="First paragraph\nSecond paragraph with complete engineering notes",ShowNotes=true,Format=new(){Size=size,Bold=true}};
                var raw=NodeMetrics.Measure(node,2);var expanded=NodeShapes.Expand(raw,shape);var content=NodeShapes.Content(shape,expanded.Width,expanded.Height);
                Assert(content.Width+.001>=raw.Width&&content.Height+.001>=raw.Height);Assert(content.X>=0&&content.Y>=0&&content.X+content.Width<=expanded.Width+.001&&content.Y+content.Height<=expanded.Height+.001);
                if(shape is NodeShape.Circle or NodeShape.Square)Assert(expanded.Width==expanded.Height);
                Assert(NodeShapes.Contour(shape,expanded.Width,expanded.Height).Segments.Count>=2);
            }
        });
        check("Mixed hierarchy shapes reflow without overlap across all ten layouts and preserve WBS identities",()=>
        {
            foreach(var layout in Enum.GetValues<ChartLayout>().Where(l=>l!=ChartLayout.Freeform))
            {
                var chart=DiagramSamples.Create(layout,DiagramPalette.MixTealOrange,NodeDesign.Rounded);chart.Shape=NodeShape.Diamond;var ids=chart.Nodes.Select(n=>n.Id).ToArray();
                chart.Nodes[1].Shape=NodeShape.Circle;chart.Nodes[2].Shape=NodeShape.Cloud;chart.Nodes[3].Shape=NodeShape.Trapezoid;chart.Nodes[4].Shape=NodeShape.Square;
                var scene=ChartGeometry.Arrange(chart);foreach(var a in scene.Boxes)foreach(var b in scene.Boxes.Where(b=>b.Id!=a.Id))Assert(!(a.X<b.X+b.Width&&a.X+a.Width>b.X&&a.Y<b.Y+b.Height&&a.Y+a.Height>b.Y));
                Assert(ids.SequenceEqual(chart.Nodes.Select(n=>n.Id)));Assert(scene.Boxes.All(b=>b.Shape==NodeShapes.Resolve(chart,chart.Nodes.Single(n=>n.Id==b.Id))));
            }
        });
        check("Graph shape changes make room while retaining the chosen anchor and all connections",()=>
        {
            var chart=NetworkCharts.Create("Shaped graph");var first=chart.Nodes[0];var second=GraphCreation.Add(chart,first.Id);var third=GraphCreation.Add(chart,second.Id);var leads=chart.Leads.Select(l=>l.Id).ToArray();var x=first.X;var y=first.Y;
            chart.Shape=NodeShape.Diamond;second.Shape=NodeShape.Circle;third.Shape=NodeShape.Cloud;NodeShapes.MakeRoom(chart,first.Id);
            var scene=ChartGeometry.Arrange(chart);foreach(var a in scene.Boxes)foreach(var b in scene.Boxes.Where(b=>b.Id!=a.Id))Assert(!FreePlacement.Intersects(a,b));
            Assert(first.X==x&&first.Y==y&&leads.SequenceEqual(chart.Leads.Select(l=>l.Id)));GraphOrganisation.Organise(chart);Charts.Validate(chart);
        });
        check("Graph lead endpoints follow actual shaped outlines on all four sides",()=>
        {
            foreach(var shape in Enum.GetValues<NodeShape>())
            {
                var box=new NodeBox(Guid.NewGuid(),40,50,300,200){Shape=shape};var target=new NodeBox(Guid.NewGuid(),800,60,200,100);
                foreach(var side in Enum.GetValues<LeadSide>().Where(s=>s!=LeadSide.Auto))foreach(var routing in Enum.GetValues<LeadRouting>())
                {
                    var point=NodeShapes.Port(box,side);Assert(point.X>=box.X-1&&point.X<=box.X+box.Width+1&&point.Y>=box.Y-1&&point.Y<=box.Y+box.Height+1);
                    var lead=LeadGeometry.Route(box,target,routing,fromSide:side);Assert(Math.Abs(lead.X1-point.X)<.001&&Math.Abs(lead.Y1-point.Y)<.001);
                }
                var automatic=LeadGeometry.Route(box,target,LeadRouting.Curve);
                Assert(Math.Abs(automatic.LabelX-(automatic.X1+3*automatic.C1X+3*automatic.C2X+automatic.X2)/8)<.001);
                Assert(Math.Abs(automatic.LabelY-(automatic.Y1+3*automatic.C1Y+3*automatic.C2Y+automatic.Y2)/8)<.001);
                Assert(automatic.LabelAngle>=-90&&automatic.LabelAngle<=90);
                if(shape==NodeShape.Trapezoid)Assert(Math.Abs(NodeShapes.Port(box,LeadSide.Right).X-(box.X+.9*box.Width))<.001);
            }
        });
        check("Shape overrides and inherited clipboard appearance survive restart backups and exports",()=>
        {
            var store=new DataStore(Path.Combine(root,"shape-persistence"));var service=new TodoService(store,store.Load());var chart=Charts.Create("Shapes",ChartLayout.TopDown);chart.Shape=NodeShape.Cloud;var child=Charts.Add(chart,chart.Nodes[0].Id,true,"Square node");child.Shape=NodeShape.Square;service.SaveDiagram(chart);
            var loaded=store.Load().Diagrams.Single();Assert(loaded.Shape==NodeShape.Cloud&&loaded.Nodes[1].Shape==NodeShape.Square);
            var copy=NodeClipboard.Copy(chart,new[]{chart.Nodes[0].Id});var target=Charts.Create("Destination",ChartLayout.TopDown);target.Shape=NodeShape.Triangle;var ids=NodeClipboard.Paste(target,copy,target.Nodes[0].Id);Assert(target.Nodes.Single(n=>n.Id==ids[0]).Shape==NodeShape.Cloud&&target.Nodes.Single(n=>n.Id==ids[1]).Shape==NodeShape.Square);
            var backup=Path.Combine(root,"shape.sumapp");service.Export(backup);service.DeleteDiagram(chart.Id);service.Restore(backup);Assert(service.Data.Diagrams.Single().Shape==NodeShape.Cloud);
            var graph=NetworkCharts.Create("CSV shapes");graph.Shape=NodeShape.Diamond;Assert(System.Text.Encoding.UTF8.GetString(NetworkExports.Export(graph,ChartExportFormat.NetworkCsv)).Contains("Diamond"));Assert(System.Text.Encoding.UTF8.GetString(NetworkExports.Export(graph,ChartExportFormat.NetworkXml)).Contains("shape=\"Diamond\""));
        });
        check("PDF uses shared shape contours and complete title and note text within content bounds",()=>
        {
            foreach(var shape in Enum.GetValues<NodeShape>())
            {
                var chart=Charts.Create("Shaped export",ChartLayout.TopDown);chart.Shape=shape;var node=chart.Nodes[0];node.Title="Complete engineering title";node.ShowNotes=true;node.Notes="First full note line\nSecond full note line";node.Format.Size=28;
                var scene=ChartGeometry.Arrange(chart,true);var box=scene.Boxes.Single();var content=NodeShapes.Content(shape,box.Width,box.Height);var drawing=DiagramPdf.Compose(chart,new(IncludeDetails:false));var marks=drawing.Pages[0].Layers[1].Marks;
                Assert(string.Join(" ",marks.OfType<PdfText>().SelectMany(t=>t.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries))).Contains(node.Title),shape+" title: "+string.Join(" | ",marks.OfType<PdfText>().Select(t=>t.Text)));Assert(string.Join(" ",marks.OfType<PdfText>().SelectMany(t=>t.Text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries))).Contains("Second full note line"),shape+" notes are incomplete");
                if(shape!=NodeShape.Design)Assert(marks.OfType<PdfNodeShape>().Single().Shape==shape);
                foreach(var text in marks.OfType<PdfText>())Assert(text.X>=box.X+content.X&&text.Y<=box.Y+content.Y+content.Height,shape+" text bounds: "+text);
                Assert(DiagramPdf.Write(drawing).Length>1000);
            }
        });
        check("Unlink all removes incoming outgoing double and parallel leads without deleting nodes",()=>
        {
            var chart=NetworkCharts.Create("Unlink");var a=chart.Nodes[0];var b=GraphCreation.Add(chart,a.Id);var c=GraphCreation.Add(chart,b.Id);NetworkCharts.Connect(chart,c.Id,a.Id,true);NetworkCharts.Connect(chart,a.Id,b.Id);
            var snapshot=chart.Clone();var nodes=JsonSerializer.Serialize(chart.Nodes,DataDocument.JsonOptions);var remaining=chart.Leads.Single(l=>l.From==b.Id&&l.To==c.Id).Id;
            Assert(GraphConnections.Unlink(chart,new[]{a.Id})==3);Assert(chart.Leads.Single().Id==remaining&&JsonSerializer.Serialize(chart.Nodes,DataDocument.JsonOptions)==nodes);Assert(snapshot.Leads.Count==4);Reject(()=>GraphConnections.Unlink(chart,new[]{Guid.NewGuid()}));Assert(chart.Leads.Count==1);
        });
        check("All eight register presets maintain normal selected header and overdue contrast",()=>
        {
            foreach(var palette in Enum.GetValues<RegisterPalette>())
            {
                var c=RegisterAppearance.Colours(palette);foreach(var background in new[]{c.Row,c.Alternate,c.Selected}){Assert(DiagramAppearance.Contrast(c.Text,background)>=4.5);Assert(DiagramAppearance.Contrast(c.Error,background)>=4.5);}Assert(DiagramAppearance.Contrast(c.HeaderText,c.Header)>=4.5);
            }
        });
        check("Register designs and ribbon mode persist with window settings and reject invalid values atomically",()=>
        {
            var store=new DataStore(Path.Combine(root,"register-settings"));var service=new TodoService(store,store.Load());service.Change(d=>{d.Settings.RegisterPalette=RegisterPalette.Charcoal;d.Settings.RegisterLayout=RegisterLayout.Compact;d.Settings.RibbonMode=RibbonDisplayMode.Compact;});
            var read=store.Load().Settings;Assert(read.RegisterPalette==RegisterPalette.Charcoal&&read.RegisterLayout==RegisterLayout.Compact&&read.RibbonMode==RibbonDisplayMode.Compact);
            var previous=File.ReadAllText(store.DataPath);Reject(()=>service.Change(d=>d.Settings.RibbonMode=(RibbonDisplayMode)99));Assert(File.ReadAllText(store.DataPath)==previous);
            var old=JsonSerializer.SerializeToNode(service.Data,DataDocument.JsonOptions)!;var settings=old["Settings"]!.AsObject();settings.Remove("RibbonMode");settings.Remove("RegisterPalette");settings.Remove("RegisterLayout");var legacy=old.Deserialize<DataDocument>(DataDocument.JsonOptions)!;Validation.Document(legacy);Assert(legacy.Settings.RibbonMode==RibbonDisplayMode.Pinned&&legacy.Settings.RegisterPalette==RegisterPalette.Sumapp);
        });
        check("Legacy custom logos migrate once to diagram branding without replacing existing company logos",()=>
        {
            var logo=File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"TestLogo.png"));var data=new DataDocument {BrandLogoPng=logo};var first=NetworkCharts.Create("Company");var existing=Charts.Create("WBS",ChartLayout.TopDown);existing.OrganisationLogoPng=logo.ToArray();existing.OrganisationLogoWidth=180;data.Diagrams.AddRange([first,existing]);
            Assert(OrganisationBranding.MigrateLegacyLogo(data));Assert(first.OrganisationLogoPng.SequenceEqual(logo)&&existing.OrganisationLogoWidth==180&&data.BrandLogoPng.SequenceEqual(logo));var unselected=NetworkCharts.Create("Other graph");var selected=NetworkCharts.Create("Selected graph");var many=new DataDocument {BrandLogoPng=logo,Diagrams=[unselected,selected]};many.Settings.SelectedNetworkId=selected.Id;Assert(OrganisationBranding.MigrateLegacyLogo(many));Assert(selected.OrganisationLogoPng.SequenceEqual(logo)&&unselected.OrganisationLogoPng.Length==0);
            first.OrganisationLogoPng=[];Assert(!OrganisationBranding.MigrateLegacyLogo(data));Assert(first.OrganisationLogoPng.Length==0);Validation.Document(data);
        });
        check("Invalid shape choices preserve saved diagrams and legacy shape values default safely",()=>
        {
            var store=new DataStore(Path.Combine(root,"shape-validation"));var service=new TodoService(store,store.Load());var chart=Charts.Create("Saved",ChartLayout.TopDown);service.SaveDiagram(chart);var before=File.ReadAllText(store.DataPath);
            Reject(()=>service.Change(d=>d.Diagrams[0].Shape=(NodeShape)999));Assert(before==File.ReadAllText(store.DataPath));Reject(()=>NodeShapes.Set(chart,new[]{Guid.NewGuid()},NodeShape.Circle));
            var old=JsonSerializer.SerializeToNode(chart,DataDocument.JsonOptions)!;old.AsObject().Remove("Shape");foreach(var node in old["Nodes"]!.AsArray())node!.AsObject().Remove("Shape");var legacy=old.Deserialize<Diagram>(DataDocument.JsonOptions)!;Charts.Validate(legacy);Assert(legacy.Shape==NodeShape.Design&&legacy.Nodes.All(n=>n.Shape is null));
        });
    }
    internal static void WriteFixtures(string directory)
    {
        var examples=new List<object>();
        foreach(var shape in Enum.GetValues<NodeShape>())
        {
            var chart=Charts.Create("Shape · "+NodeShapes.Name(shape),ChartLayout.TopDown);chart.Shape=shape;chart.Nodes[0].Title="Engineering review";var child=Charts.Add(chart,chart.Nodes[0].Id,true,"Complete notes");child.ShowNotes=true;child.Notes="First note paragraph\nSecond note paragraph";
            var drawing=DiagramPdf.Compose(chart,new(IncludeDetails:false));
            File.WriteAllBytes(Path.Combine(directory,"shape-"+shape+".pdf"),DiagramPdf.Write(drawing));
            examples.Add(new {Name=NodeShapes.Name(shape),Example=new {Scene=ChartGeometry.Arrange(chart),Marks=drawing.Pages[0].Layers[1].Marks.Select(m=>new {Kind=m.GetType().Name,Data=(object)m})}});
        }
        File.WriteAllText(Path.Combine(directory,"SUMAPP-shapes.json"),JsonSerializer.Serialize(new {Shapes=examples,Registers=Enum.GetValues<RegisterPalette>().Select(p=>RegisterAppearance.Colours(p))},DataDocument.JsonOptions));
    }
}
