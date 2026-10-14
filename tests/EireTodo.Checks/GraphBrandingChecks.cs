using System.Text.Json;
using System.Xml.Linq;
using System.Text;
using EireTodo.Core;
using PdfSharp.Pdf.IO;

internal static class GraphBrandingChecks
{
    private static void Assert(bool value) { if(!value)throw new Exception("Graph branding assertion failed."); }
    private static void Reject(Action action) { try{action();}catch(Exception ex) when(ex is ArgumentException or IOException){return;}throw new Exception("Expected rejection."); }
    private static byte[] Logo => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"TestLogo.png"));
    public static void Run(Action<string,Action> check,string root)
    {
        check("Graph nodes keep uniform colour, dimensions and existing positions when leads change direction",()=>
        {
            var graph=NetworkCharts.Create("Graph");var selected=graph.Nodes[0];selected.Title="A wrapped title for an engineering document";selected.Notes="Notes";
            var before=ChartGeometry.Arrange(graph).Boxes.Single();
            var outgoing=GraphCreation.Add(graph,selected.Id,"Outgoing",LeadDirection.Outgoing);
            var incoming=GraphCreation.Add(graph,selected.Id,"Incoming",LeadDirection.Incoming);
            var paired=GraphCreation.Add(graph,selected.Id,"Paired",LeadDirection.Both);
            Assert(graph.Leads.Any(l=>l.From==selected.Id&&l.To==outgoing.Id&&!l.DoubleHeaded));Assert(graph.Leads.Any(l=>l.From==incoming.Id&&l.To==selected.Id));Assert(graph.Leads.Any(l=>l.From==selected.Id&&l.To==paired.Id&&l.DoubleHeaded));
            var after=ChartGeometry.Arrange(graph).Boxes.Single(b=>b.Id==selected.Id);Assert(before==after);
            foreach(var palette in Enum.GetValues<DiagramPalette>()) { graph.Palette=palette;Assert(graph.Nodes.Select(n=>DiagramAppearance.Fill(graph,n,DiagramAppearance.Levels(graph)[n.Id])).Distinct().Count()==1); }
        });
        check("Manual colours affect only selected nodes, remain readable, reset and survive clipboard and restart",()=>
        {
            var store=new DataStore(Path.Combine(root,"graph-colour"));var service=new TodoService(store,store.Load());var graph=NetworkCharts.Create("Colours");var second=GraphCreation.Add(graph,graph.Nodes[0].Id,"Second");var third=GraphCreation.Add(graph,second.Id,"Third");
            NodeColours.Set(graph,new[]{second.Id,third.Id},"#DC2626");Assert(graph.Nodes[0].FillColour is null && second.FillColour==third.FillColour);
            foreach(var colour in new[]{"#DC2626","#EA580C","#E5E7EB","#1F2937","#FFCC33"}) {NodeColours.Set(graph,new[]{second.Id},colour);foreach(var design in Enum.GetValues<NodeDesign>()){graph.Design=design;var surface=DiagramAppearance.Surface(graph,second,2);Assert(DiagramAppearance.Contrast(surface.Text,surface.Fill)>=4.5);}}
            graph.Design=NodeDesign.Cards;NodeColours.Set(graph,new[]{second.Id},"#DC2626");service.SaveDiagram(graph);Assert(store.Load().Diagrams.Single().Nodes.Single(n=>n.Id==second.Id).FillColour=="#DC2626");
            var pasted=NodeClipboard.Paste(graph,NodeClipboard.Copy(graph,new[]{second.Id}),graph.Nodes[0].Id);Assert(graph.Nodes.Single(n=>n.Id==pasted[0]).FillColour=="#DC2626");NodeColours.Set(graph,pasted,null);Assert(graph.Nodes.Single(n=>n.Id==pasted[0]).FillColour is null);
            Reject(()=>NodeColours.Set(graph,new[]{second.Id},"red"));Reject(()=>NodeColours.Set(graph,new[]{Guid.NewGuid()},"#000000"));
            var xml=XDocument.Load(new MemoryStream(NetworkExports.Export(graph,ChartExportFormat.NetworkXml)));XNamespace ns=NetworkExports.XmlNamespace;Assert(xml.Descendants(ns+"Node").Single(n=>(string?)n.Attribute("id")==second.Id.ToString()).Attribute("fillColour")!.Value=="#DC2626");
            Assert(Encoding.UTF8.GetString(NetworkExports.Export(graph,ChartExportFormat.NetworkCsv)).Contains("Node Fill Colour"));
        });
        check("Organisation logo bytes and independent sizes survive profile restart, backup and restore",()=>
        {
            var store=new DataStore(Path.Combine(root,"organisation-logo"));var service=new TodoService(store,store.Load());var graph=NetworkCharts.Create("Company graph");graph.OrganisationLogoPng=Logo;graph.OrganisationLogoWidth=220;graph.ExportLogoWidth=125;service.SaveDiagram(graph);
            var loaded=store.Load().Diagrams.Single();Assert(loaded.OrganisationLogoPng.SequenceEqual(Logo)&&loaded.OrganisationLogoWidth==220&&loaded.ExportLogoWidth==125);
            var backup=Path.Combine(root,"organisation.sumapp");service.Export(backup);service.DeleteDiagram(graph.Id);service.Restore(backup);Assert(service.Data.Diagrams.Single().OrganisationLogoPng.SequenceEqual(Logo));
            var legacy=JsonSerializer.SerializeToNode(graph,DataDocument.JsonOptions)!;legacy.AsObject().Remove("OrganisationLogoPng");legacy.AsObject().Remove("OrganisationLogoWidth");legacy.AsObject().Remove("ExportLogoWidth");var old=legacy.Deserialize<Diagram>(DataDocument.JsonOptions)!;Charts.Validate(old);Assert(old.OrganisationLogoPng.Length==0&&old.OrganisationLogoWidth==140&&old.ExportLogoWidth==90);
        });
        check("Organisation logos preserve proportions and reserve clear PDF headers in both paper orientations",()=>
        {
            var graph=NetworkCharts.Create("Logo and export");graph.OrganisationLogoPng=Logo;graph.ExportLogoWidth=150;GraphCreation.Add(graph,graph.Nodes[0].Id,"Engineering notes");
            foreach(var orientation in Enum.GetValues<PdfOrientation>()) foreach(var paper in Enum.GetValues<PdfPaper>())
            {
                var drawing=DiagramPdf.Compose(graph,new(paper,orientation,IncludeDetails:true));foreach(var page in drawing.Pages) {var image=page.Layers[0].Marks.OfType<PdfImage>().Single();Assert(image.X==28&&image.Y==28&&image.Png.SequenceEqual(Logo));Assert(image.Width<=150&&image.Height<=120);if(page.Layers.Count>1)Assert(page.Layers[1].Clip!.Y>=image.Y+image.Height+8);}
                using var stream=new MemoryStream(DiagramPdf.Write(drawing));using var pdf=PdfReader.Open(stream,PdfDocumentOpenMode.Import);Assert(pdf.PageCount==drawing.Pages.Count);
            }
            var size=OrganisationBranding.Fit(Logo,140,300);Assert(size.Width>0&&size.Height>0&&size.Width<=140&&size.Height<=300);
        });
        check("Invalid organisation images, sizes and colours leave previously saved data unchanged",()=>
        {
            var store=new DataStore(Path.Combine(root,"logo-invalid"));var service=new TodoService(store,store.Load());var graph=NetworkCharts.Create("Keep data");service.SaveDiagram(graph);var previous=File.ReadAllText(store.DataPath);
            var invalid=graph.Clone();invalid.OrganisationLogoPng=new byte[]{1,2,3};Reject(()=>service.SaveDiagram(invalid));invalid=graph.Clone();invalid.ExportLogoWidth=double.NaN;Reject(()=>service.SaveDiagram(invalid));invalid=graph.Clone();invalid.OrganisationLogoWidth=900;Reject(()=>service.SaveDiagram(invalid));invalid=graph.Clone();invalid.Nodes[0].FillColour="invalid";Reject(()=>service.SaveDiagram(invalid));Assert(File.ReadAllText(store.DataPath)==previous&&service.Data.Diagrams.Single().Nodes[0].Title==graph.Nodes[0].Title);
        });
        check("Organise graph respects directed groups, cycles, disconnected nodes, sizes and stable identities",()=>
        {
            var graph=NetworkCharts.Create("Organise");var a=graph.Nodes[0];var b=GraphCreation.Add(graph,a.Id,"Long engineering document title that wraps over multiple lines");b.ShowNotes=true;b.Notes="Detailed notes\nSecond paragraph\nThird line";var c=GraphCreation.Add(graph,b.Id,"Third");GraphCreation.Connect(graph,c.Id,b.Id,LeadDirection.Outgoing,LeadRouting.Curve);var d=GraphCreation.Add(graph,null,"Disconnected",unlinked:true);d.Format.Size=32;NodeColours.Set(graph,new[]{b.Id},"#991B1B");graph.OrganisationLogoPng=Logo;
            var before=graph.Clone();var first=GraphOrganisation.Arrange(graph,preferredWidth:1000);var again=GraphOrganisation.Arrange(graph,preferredWidth:1000);Assert(first.Boxes.SequenceEqual(again.Boxes));
            foreach(var x in first.Boxes)foreach(var y in first.Boxes.Where(y=>y.Id!=x.Id))Assert(!FreePlacement.Intersects(x,y));
            GraphOrganisation.Organise(graph,preferredWidth:1000);Assert(JsonSerializer.Serialize(graph.Leads,DataDocument.JsonOptions)==JsonSerializer.Serialize(before.Leads,DataDocument.JsonOptions));Assert(graph.Nodes.Select(n=>n.Id).SequenceEqual(before.Nodes.Select(n=>n.Id)));Assert(graph.Nodes.Single(n=>n.Id==b.Id).FillColour=="#991B1B"&&graph.Nodes.Single(n=>n.Id==b.Id).Notes==b.Notes&&graph.OrganisationLogoPng.SequenceEqual(Logo));
            var store=new DataStore(Path.Combine(root,"organise"));var service=new TodoService(store,store.Load());service.SaveDiagram(graph);Assert(store.Load().Diagrams.Single().Nodes.Select(n=>(n.Id,n.X,n.Y)).SequenceEqual(graph.Nodes.Select(n=>(n.Id,n.X,n.Y))));service.SaveDiagram(before);Assert(store.Load().Diagrams.Single().Nodes.Select(n=>(n.X,n.Y)).SequenceEqual(before.Nodes.Select(n=>(n.X,n.Y))));
        });
        check("Organise graph handles a 1,000-node cycle and rejects invalid layouts without mutation",()=>
        {
            var graph=new Diagram {Kind=DiagramKind.Network,Layout=ChartLayout.Freeform,Name="Large cycle",NextNumber=1001};
            graph.Nodes=Enumerable.Range(1,1000).Select(i=>new ChartNode {Number=i,Order=i-1,Title="Node "+i,X=40,Y=40}).ToList();graph.Leads=Enumerable.Range(0,1000).Select(i=>new DiagramLead {From=graph.Nodes[i].Id,To=graph.Nodes[(i+1)%1000].Id}).ToList();
            var scene=GraphOrganisation.Arrange(graph);Assert(scene.Boxes.Count==1000&&scene.Boxes.All(b=>double.IsFinite(b.X)&&double.IsFinite(b.Y)&&Math.Abs(b.X)<=100000&&Math.Abs(b.Y)<=100000));var sorted=scene.Boxes.OrderBy(b=>b.Y).ToList();for(int i=1;i<sorted.Count;i++)Assert(!FreePlacement.Intersects(sorted[i-1],sorted[i]));
            var before=JsonSerializer.Serialize(graph,DataDocument.JsonOptions);Reject(()=>GraphOrganisation.Organise(graph,preferredWidth:double.NaN));Assert(JsonSerializer.Serialize(graph,DataDocument.JsonOptions)==before);Reject(()=>GraphOrganisation.Organise(Charts.Create("Hierarchy",ChartLayout.MindMap)));
        });
        check("Graph Enter peers preserve unlinked and outgoing patterns and support both incoming interpretations",()=>
        {
            var graph=NetworkCharts.Create("Peers");var rootNode=graph.Nodes[0];var unlinked=GraphPeers.Add(graph,rootNode.Id);Assert(graph.Leads.Count==0&&unlinked.Id!=rootNode.Id);
            var source=GraphCreation.Add(graph,rootNode.Id,"Source",LeadDirection.Incoming);var peer=GraphPeers.Add(graph,source.Id);Assert(graph.Leads.Any(l=>l.From==peer.Id&&l.To==rootNode.Id)&&!graph.Leads.Any(l=>l.From==source.Id&&l.To==peer.Id));
            var incomingFromSelected=GraphPeers.Add(graph,rootNode.Id,IncomingPeerRule.FromSelectedNode);Assert(graph.Leads.Any(l=>l.From==rootNode.Id&&l.To==incomingFromSelected.Id));
            var sameSource=GraphPeers.Add(graph,incomingFromSelected.Id,IncomingPeerRule.SameSource);Assert(graph.Leads.Any(l=>l.From==rootNode.Id&&l.To==sameSource.Id)&&!graph.Leads.Any(l=>l.From==incomingFromSelected.Id&&l.To==sameSource.Id));
            var boxes=ChartGeometry.Arrange(graph).Boxes;foreach(var a in boxes)foreach(var b in boxes.Where(b=>b.Id!=a.Id))Assert(!FreePlacement.Intersects(a,b));
        });
        check("Graph peers retain existing labels, routes, identities and manual colours while allocating fresh links",()=>
        {
            var graph=NetworkCharts.Create("Peer relationships");var a=graph.Nodes[0];var b=GraphCreation.Add(graph,a.Id,"Target");var original=graph.Leads[0];original.Description="Same relationship";original.Routing=LeadRouting.SharpBends;original.FromSide=LeadSide.Bottom;a.FillColour="#991B1B";
            var before=graph.Clone();var peer=GraphPeers.Add(graph,a.Id);var newLead=graph.Leads.Single(l=>l.From==peer.Id);Assert(newLead.To==b.Id&&newLead.Id!=original.Id&&newLead.Description==original.Description&&newLead.Routing==original.Routing&&newLead.FromSide==original.FromSide);Assert(peer.FillColour is null&&graph.Nodes.Single(n=>n.Id==a.Id).FillColour=="#991B1B");Assert(graph.Leads.Single(l=>l.Id==original.Id).From==before.Leads[0].From);
        });
        check("Bulk connections support many incoming outgoing and double leads with explicit duplicate handling",()=>
        {
            var graph=NetworkCharts.Create("Bulk");var anchor=graph.Nodes[0];var ids=Enumerable.Range(1,8).Select(i=>GraphCreation.Add(graph,null,"Node "+i,unlinked:true).Id).ToList();
            Assert(GraphConnections.ConnectMany(graph,anchor.Id,ids,LeadDirection.Outgoing,LeadRouting.Curve,"Supplies")==8);Assert(GraphConnections.ConnectMany(graph,anchor.Id,ids,LeadDirection.Outgoing,LeadRouting.Curve)==0);
            Assert(GraphConnections.ConnectMany(graph,anchor.Id,ids,LeadDirection.Incoming,LeadRouting.SharpBends)==8);Assert(GraphConnections.ConnectMany(graph,anchor.Id,ids,LeadDirection.Both,LeadRouting.Curve)==8);
            Assert(graph.Leads.Count(l=>l.DoubleHeaded)==8&&graph.Leads.Count(l=>l.Description=="Supplies")==8);Assert(GraphConnections.ConnectMany(graph,anchor.Id,new[]{ids[0]},LeadDirection.Outgoing,LeadRouting.Curve,parallel:true)==1);Charts.Validate(graph);
        });
        check("Failed bulk connections preserve all data and a complete batch is one saved snapshot",()=>
        {
            var graph=NetworkCharts.Create("Atomic batch");var anchor=graph.Nodes[0];var other=GraphCreation.Add(graph,null,"Other",unlinked:true);var before=JsonSerializer.Serialize(graph,DataDocument.JsonOptions);
            Reject(()=>GraphConnections.ConnectMany(graph,anchor.Id,new[]{other.Id,Guid.NewGuid()},LeadDirection.Outgoing,LeadRouting.Curve));Reject(()=>GraphConnections.ConnectMany(graph,anchor.Id,new[]{anchor.Id},LeadDirection.Incoming,LeadRouting.Curve));Assert(JsonSerializer.Serialize(graph,DataDocument.JsonOptions)==before);
            var store=new DataStore(Path.Combine(root,"bulk-connections"));var service=new TodoService(store,store.Load());service.SaveDiagram(graph);var old=graph.Clone();GraphConnections.ConnectMany(graph,anchor.Id,new[]{other.Id},LeadDirection.Both,LeadRouting.SharpBends,"Shared direction");service.SaveDiagram(graph);Assert(store.Load().Diagrams.Single().Leads.Single().DoubleHeaded);service.SaveDiagram(old);Assert(store.Load().Diagrams.Single().Leads.Count==0);
        });
        check("Explicit multi-colour packs style one graph without automatic colouring of newly added nodes",()=>
        {
            var graph=NetworkCharts.Create("Multi design");for(int i=0;i<5;i++)GraphCreation.Add(graph,null,"Node "+i,unlinked:true);var ids=graph.Nodes.Select(n=>n.Id).ToArray();
            foreach(var palette in Enum.GetValues<DiagramPalette>().Where(NodeColours.IsMulticolour))
            {
                NodeColours.ApplyPackage(graph,palette);Assert(graph.Nodes.Take(6).Select(n=>n.FillColour).Distinct().Count()==3);Assert(graph.Nodes.Take(6).All(n=>n.ColourFromPreset));
                foreach(var design in Enum.GetValues<NodeDesign>()){graph.Design=design;foreach(var node in graph.Nodes){var surface=DiagramAppearance.Surface(graph,node,2);Assert(DiagramAppearance.Contrast(surface.Text,surface.Fill)>=4.5);}}
            }
            var added=GraphPeers.Add(graph,graph.Nodes[0].Id);Assert(added.FillColour is null&&!added.ColourFromPreset);NodeColours.Set(graph,new[]{ids[0]},"#7F1D1D");NodeColours.ApplyPackage(graph,DiagramPalette.Grey);Assert(graph.Nodes.Single(n=>n.Id==ids[0]).FillColour=="#7F1D1D"&&graph.Nodes.Where(n=>n.Id!=ids[0]).All(n=>n.FillColour is null));
            var restarted=JsonSerializer.Deserialize<Diagram>(JsonSerializer.Serialize(graph,DataDocument.JsonOptions),DataDocument.JsonOptions)!;Charts.Validate(restarted);Assert(restarted.Nodes[0].FillColour=="#7F1D1D");
        });
        check("Red orange grey charcoal and combination presets keep stable legacy IDs and readable states",()=>
        {
            Assert((int)DiagramPalette.Olive==20&&(int)DiagramPalette.Plum==15&&Enum.GetValues<DiagramPalette>().Length==34);
            var graph=NetworkCharts.Create("Presets");var node=graph.Nodes[0];
            foreach(var palette in new[]{DiagramPalette.Red,DiagramPalette.Orange,DiagramPalette.Grey,DiagramPalette.Charcoal,DiagramPalette.RedCharcoal,DiagramPalette.OrangeGrey,DiagramPalette.Ember})foreach(var design in Enum.GetValues<NodeDesign>())
            {
                graph.Palette=palette;graph.Design=design;foreach(var due in new[]{false,true})foreach(var priority in new[]{false,true}){node.Priority=priority;node.FinishDate=due?DateOnly.FromDateTime(DateTime.Today.AddDays(-1)):null;var surface=DiagramAppearance.Surface(graph,node,2);Assert(DiagramAppearance.Contrast(surface.Text,surface.Fill)>=4.5);}
            }
        });
    }
}
