using System.Text.Json;
using EireTodo.Core;

internal static class ClipboardChecks
{
    private static void Assert(bool value) { if (!value) throw new Exception("Clipboard / notes assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Expected rejection."); }
    public static void Run(Action<string,Action> check,string root)
    {
        check("Multiple copied branches deduplicate descendants and paste as children with fresh identities",() =>
        {
            var chart=Charts.Create("Root",ChartLayout.MindMap); var parent=chart.Nodes[0];
            var first=Charts.Add(chart,parent.Id,true,"First"); var child=Charts.Add(chart,first.Id,true,"Nested"); var last=Charts.Add(chart,parent.Id,true,"Last");
            child.Notes="Full\nmultiline notes Ω"; child.ShowNotes=true; child.Format.Bold=true; child.Priority=true;
            var before=JsonSerializer.Serialize(chart,DataDocument.JsonOptions);
            var copied=NodeClipboard.Copy(chart,new[] { first.Id,child.Id,last.Id }); Assert(copied.Nodes.Count==3);
            var pasted=NodeClipboard.Paste(chart,copied,parent.Id); Assert(pasted.Count==3 && chart.Nodes.Count==7);
            Assert(pasted.All(id => !copied.Nodes.Any(n => n.Id==id)) && chart.Nodes.Select(n => n.Number).Distinct().Count()==7);
            var nested=chart.Nodes.Single(n => pasted.Contains(n.Id) && n.Title=="Nested");
            Assert(nested.ShowNotes && nested.Notes==child.Notes && nested.Format.Bold && nested.Priority);
            var firstCopy=chart.Nodes.Single(n => pasted.Contains(n.Id) && n.Title=="First"); Assert(nested.ParentId==firstCopy.Id && firstCopy.ParentId==parent.Id);
            Assert(Charts.Outline(chart).Single(o => o.Node.Id==firstCopy.Id).Code=="1.3");
            Assert(JsonSerializer.Serialize(copied,DataDocument.JsonOptions)!=before && chart.Nodes.Single(n=>n.Id==child.Id).Notes==child.Notes);
            var again=NodeClipboard.Paste(chart,copied,parent.Id); Assert(!again.Intersect(pasted).Any());
        });
        check("Graph copying preserves internal labelled leads and paste connects to the selected node",() =>
        {
            var graph=NetworkCharts.Create("Graph"); var a=graph.Nodes[0]; var b=GraphCreation.Add(graph,a.Id,"Linked");
            a.ShowNotes=true; a.Notes=string.Join("\n",Enumerable.Repeat(new string('W',120),20));
            graph.Leads[0].Description="Dependency Ω"; graph.Leads[0].DoubleHeaded=true; graph.Leads[0].FromSide=LeadSide.Top;
            var c=GraphCreation.Add(graph,b.Id,"Outside selection"); var copied=NodeClipboard.Copy(graph,new[] { a.Id,b.Id });
            Assert(copied.Nodes.Count==2 && copied.Leads.Count==1);
            var pasted=NodeClipboard.Paste(graph,copied,c.Id); Charts.Validate(graph);
            Assert(graph.Leads.Any(l => pasted.Contains(l.From) && pasted.Contains(l.To) && l.Description=="Dependency Ω" && l.DoubleHeaded && l.FromSide==LeadSide.Top));
            Assert(graph.Leads.Count(l => l.From==c.Id && pasted.Contains(l.To))==2);
            var boxes=ChartGeometry.Arrange(graph).Boxes; foreach(var id in pasted) foreach(var original in boxes.Where(x => !pasted.Contains(x.Id))) Assert(!FreePlacement.Intersects(boxes.Single(x=>x.Id==id),original));
            Assert(!FreePlacement.Intersects(boxes.Single(x=>x.Id==pasted[0]),boxes.Single(x=>x.Id==pasted[1])));
        });
        check("Invalid, oversized and excessive-depth paste leave the original diagram unchanged",() =>
        {
            var target=Charts.Create("Target",ChartLayout.TopDown); var source=Charts.Create("Copy",ChartLayout.TopDown); Guid tip=target.Nodes[0].Id;
            for(int i=1;i<Charts.MaxDepth;i++) tip=Charts.Add(target,tip,true,"Level "+i).Id;
            var before=JsonSerializer.Serialize(target,DataDocument.JsonOptions); Reject(()=>NodeClipboard.Paste(target,source,tip)); Assert(JsonSerializer.Serialize(target,DataDocument.JsonOptions)==before);
            source.Nodes[0].Title=""; Reject(()=>NodeClipboard.Paste(target,source,null)); Assert(JsonSerializer.Serialize(target,DataDocument.JsonOptions)==before);
            source.Nodes[0].Title="Copy"; var big=Charts.Create("Full",ChartLayout.Outline); for(int i=1;i<Charts.MaxNodes;i++) Charts.Add(big,null,false,"Node "+i);
            before=JsonSerializer.Serialize(big,DataDocument.JsonOptions); Reject(()=>NodeClipboard.Paste(big,source,null)); Assert(JsonSerializer.Serialize(big,DataDocument.JsonOptions)==before);
        });
        check("Copied nodes, full notes, new appearance and folds persist and support saved-snapshot undo",() =>
        {
            var store=new DataStore(Path.Combine(root,"clipboard-restart")); var service=new TodoService(store,store.Load());
            var chart=Charts.Create("Persist",ChartLayout.BottomUp); chart.Palette=DiagramPalette.Lagoon; chart.Design=NodeDesign.TopAccent; var node=Charts.Add(chart,chart.Nodes[0].Id,true,"Notes"); node.ShowNotes=true; node.Notes="Line one\nLine two"; node.Collapsed=true;
            service.SaveDiagram(chart); var before=chart.Clone(); var pasted=NodeClipboard.Paste(chart,NodeClipboard.Copy(chart,new[]{node.Id}),chart.Nodes[0].Id); service.SaveDiagram(chart);
            var loaded=store.Load().Diagrams.Single(); Assert(loaded.Nodes.Any(n=>pasted.Contains(n.Id)&&n.ShowNotes&&n.Notes==node.Notes&&n.Collapsed)); Assert(loaded.Layout==ChartLayout.BottomUp && loaded.Palette==DiagramPalette.Lagoon && loaded.Design==NodeDesign.TopAccent);
            service.SaveDiagram(before); Assert(store.Load().Diagrams.Single().Nodes.Count==2);
            var legacyJson=JsonSerializer.SerializeToNode(before,DataDocument.JsonOptions)!; foreach(var item in legacyJson["Nodes"]!.AsArray()) item!.AsObject().Remove("ShowNotes");
            var legacy=legacyJson.Deserialize<Diagram>(DataDocument.JsonOptions)!; Charts.Validate(legacy); Assert(legacy.Nodes.All(n=>!n.ShowNotes));
        });
        check("Complete node notes reserve space, appear in fitted PDF and preserve paragraph breaks",() =>
        {
            var chart=Charts.Create("Full notes",ChartLayout.TopDown); var node=chart.Nodes[0]; node.Notes=string.Join("\n",Enumerable.Range(1,25).Select(i=>"Notes line "+i+" Ω"));
            var preview=NodeMetrics.Measure(node); node.ShowNotes=true; var full=NodeMetrics.Measure(node); Assert(full.Height>preview.Height+200);
            var drawing=DiagramPdf.Compose(chart,new(IncludeDetails:false)); var notes=drawing.Pages[0].Layers[1].Marks.OfType<PdfText>().ToList(); Assert(notes.Any(t=>t.Text.Contains("Notes line 25")));
            Assert(notes.Max(t=>t.Y)<=ChartGeometry.Arrange(chart).Boxes[0].Height+ChartGeometry.Margin);
            node.Notes="Changed notes"; Assert(NodeMetrics.Measure(node).Height<full.Height);
            var hidden=Charts.Add(chart,node.Id,true,"Folded child stays exported"); FreePlacement.Enable(chart); node=chart.Nodes.Single(n=>n.Id==node.Id); node.Collapsed=true;
            var folded=DiagramPdf.Compose(chart,new(IncludeDetails:false)); Assert(OpenWorkspace.CompactGraph(chart,true).Boxes.Any(b=>b.Id==hidden.Id));
            Assert(string.Concat(folded.Pages[0].Layers[1].Marks.OfType<PdfText>().Select(t=>t.Text)).Contains(hidden.Title));
        });
        check("Ten hierarchy layouts keep measured nodes separated and have distinct geometry",() =>
        {
            Assert(Enum.GetValues<ChartLayout>().Count(l=>l!=ChartLayout.Freeform)==10);
            var scenes=new List<string>();
            foreach(var layout in Enum.GetValues<ChartLayout>().Where(l=>l!=ChartLayout.Freeform))
            {
                var chart=DiagramSamples.Create(layout,DiagramPalette.Olive,NodeDesign.BranchCards); chart.Nodes[^1].ShowNotes=true; chart.Nodes[^1].Notes="Notes\nsecond line\nthird line";
                var scene=ChartGeometry.Arrange(chart); for(int i=0;i<scene.Boxes.Count;i++) for(int j=i+1;j<scene.Boxes.Count;j++) { var a=scene.Boxes[i];var b=scene.Boxes[j];Assert(a.X+a.Width<=b.X||b.X+b.Width<=a.X||a.Y+a.Height<=b.Y||b.Y+b.Height<=a.Y); }
                scenes.Add(string.Join(";",scene.Boxes.Select(b=>$"{b.X},{b.Y},{b.Width},{b.Height}"))); Assert(DiagramPdf.Write(DiagramPdf.Compose(chart,new(IncludeDetails:false))).Length>1000);
            }
            // Right-tree mind map and left-to-right WBS share coordinates, with different routing.
            Assert(scenes.Distinct().Count()==9);
        });
    }
}
