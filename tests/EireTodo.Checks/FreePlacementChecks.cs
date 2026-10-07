using EireTodo.Core;
using System.Text.Json;
using System.Diagnostics;

internal static class FreePlacementChecks
{
    private static void Assert(bool value) { if (!value) throw new Exception("Free placement assertion failed."); }
    private static void Separate(ChartScene scene)
    {
        for (var i = 0; i < scene.Boxes.Count; i++) for (var j = i+1; j < scene.Boxes.Count; j++) Assert(!FreePlacement.Intersects(scene.Boxes[i],scene.Boxes[j]));
    }
    public static void Run(Action<string,Action> check,string root)
    {
        check("Free movement pins the desired position and moves nearby nodes without changing WBS structure",() =>
        {
            var chart = Charts.Create("Engineering",ChartLayout.TopDown); var parent = chart.Nodes[0].Id;
            for (var i = 0; i < 12; i++) Charts.Add(chart,parent,true,"Component " + i);
            var codes = Charts.Outline(chart).Select(o => (o.Node.Id,o.Code,o.Node.ParentId,o.Node.Order)).ToArray();
            FreePlacement.Enable(chart); var original = ChartGeometry.Arrange(chart); var moved = original.Boxes[1]; var obstacle = original.Boxes[2];
            var scene = FreePlacement.Move(original,moved.Id,obstacle.X,obstacle.Y); Separate(scene);
            Assert(scene.Boxes.Single(b => b.Id == moved.Id).X == obstacle.X && scene.Boxes.Single(b => b.Id == moved.Id).Y == obstacle.Y);
            Assert(scene.Boxes.Single(b => b.Id == obstacle.Id) != obstacle);
            Assert(scene.Boxes.Last() == original.Boxes.Last());
            Assert(scene.Boxes.SequenceEqual(FreePlacement.Move(original,moved.Id,obstacle.X,obstacle.Y).Boxes));
            FreePlacement.Commit(chart,scene); Assert(codes.SequenceEqual(Charts.Outline(chart).Select(o => (o.Node.Id,o.Code,o.Node.ParentId,o.Node.Order))));
            Separate(ChartGeometry.Arrange(chart));
            foreach(var layout in new[] { ChartLayout.MindMap,ChartLayout.TopDown })
            {
                var parentBox = scene.Boxes[0]; var above = parentBox with { Id = Guid.NewGuid(), Y = parentBox.Y - parentBox.Height - 300, X = parentBox.X };
                var connection = FreePlacement.Connection(parentBox,above,layout);
                Assert(connection.X1 == parentBox.X+parentBox.Width/2 && connection.Y1 == parentBox.Y);
                Assert(connection.Y2 == above.Y+above.Height && connection.X2 == above.X+above.Width/2);
            }
            var negative = FreePlacement.Move(scene,moved.Id,-850,-450); FreePlacement.Commit(chart,negative); Assert(ChartGeometry.Arrange(chart).Boxes.Single(b => b.Id == moved.Id).X == -850);
        });
        check("Free placement adapts to all layouts, long titles, formatting, insertion and folded branches",() =>
        {
            foreach (var layout in Enum.GetValues<ChartLayout>().Where(l => l != ChartLayout.Freeform))
            {
                var chart = DiagramSamples.Create(layout,DiagramPalette.Navy,NodeDesign.Tiered);
                var node = chart.Nodes.Last(); node.Title = new string('W',90); node.Format.Size = 48; node.Format.Bold = true;
                FreePlacement.Enable(chart); var scene = ChartGeometry.Arrange(chart); Separate(scene);
                var box = scene.Boxes.Single(b => b.Id == node.Id); FreePlacement.Commit(chart,FreePlacement.Move(scene,node.Id,-1000.25,-300.75));
                Charts.Add(chart,node.Id,true,"New child"); Separate(ChartGeometry.Arrange(chart));
                node.Collapsed = true; Separate(ChartGeometry.Arrange(chart)); node.Collapsed = false; Separate(ChartGeometry.Arrange(chart));
                chart.FreeMove = false; Assert(ChartGeometry.Arrange(chart).Boxes.All(b => b.X >= 0 && b.Y >= 0));
                chart.FreeMove = true; Assert(ChartGeometry.Arrange(chart).Boxes.Single(b => b.Id == node.Id).X == -1000.25);
                FreePlacement.Reset(chart); Assert(chart.Nodes.All(n => n.X is null && n.Y is null)); Separate(ChartGeometry.Arrange(chart));
            }
        });
        check("Free positioning and PC copies preserve live data, settings and diagrams after restart and restore",() =>
        {
            var store = new DataStore(Path.Combine(root,"free-files")); var service = new TodoService(store,store.Load());
            var chart = Charts.Create("Saved hierarchy",ChartLayout.MindMap); Charts.Add(chart,chart.Nodes[0].Id,true,"Preserved child"); FreePlacement.Enable(chart);
            var scene = ChartGeometry.Arrange(chart); FreePlacement.Commit(chart,FreePlacement.Move(scene,chart.Nodes[0].Id,-600,-400)); service.SaveDiagram(chart);
            service.SaveTask(new() { ProjectId = service.Data.Projects[0].Id, Description = "Live task" });
            var copy = Path.Combine(root,"engineering.sumapp"); service.Export(copy); var saved = File.ReadAllBytes(copy); var livePath = store.DataPath;
            service.SaveTask(new() { ProjectId = service.Data.Projects[0].Id, Description = "Later task" }); Assert(File.ReadAllBytes(copy).SequenceEqual(saved) && store.DataPath == livePath);
            var restarted = new TodoService(store,store.Load()); Assert(restarted.Data.Tasks.Count == 2 && restarted.Data.Diagrams.Single().FreeMove);
            Assert(restarted.Data.Diagrams.Single().Nodes[0].X == -600);
            restarted.Restore(copy); Assert(store.Load().Tasks.Count == 1 && store.Load().Diagrams.Single().Nodes[0].Y == -400);
            var before = File.ReadAllBytes(store.DataPath); var invalid = restarted.Data.Diagrams.Single().Clone(); invalid.Nodes[0].X = double.PositiveInfinity;
            try { restarted.SaveDiagram(invalid); throw new Exception("Invalid position accepted"); } catch (ArgumentException) { }
            Assert(before.SequenceEqual(File.ReadAllBytes(store.DataPath)));
            var legacy = JsonSerializer.Serialize(chart,DataDocument.JsonOptions).Replace("\"FreeMove\": true,","");
            Assert(!JsonSerializer.Deserialize<Diagram>(legacy,DataDocument.JsonOptions)!.FreeMove);
        });
        check("Free hierarchy PDF compacts negative positions and fits both paper orientations",() =>
        {
            var chart = Charts.Create("Negative hierarchy",ChartLayout.TopDown); var child = Charts.Add(chart,chart.Nodes[0].Id,true,"Full title on paper"); FreePlacement.Enable(chart);
            var scene = ChartGeometry.Arrange(chart); FreePlacement.Commit(chart,FreePlacement.Move(scene,child.Id,-800,-600));
            var compact = OpenWorkspace.CompactGraph(chart); Assert(compact.Boxes.All(b => b.X >= ChartGeometry.Margin && b.Y >= ChartGeometry.Margin));
            foreach(var orientation in Enum.GetValues<PdfOrientation>())
            {
                var drawing = DiagramPdf.Compose(chart,new(Orientation:orientation,IncludeDetails:false)); Assert(drawing.Pages.Count == 1);
                var layer = drawing.Pages.Single().Layers.Single(l => l.Marks.OfType<PdfBox>().Any());
                Assert(layer.Marks.OfType<PdfBox>().All(b => b.X >= 0 && b.Y >= 0));
                Assert(layer.Marks.OfType<PdfText>().Any(t => t.Text.Contains("Full title")));
            }
        });
        check("Desktop logo PNG bytes survive restart, PC copies and restore without changing tasks",() =>
        {
            // Valid 1×1 PNG; use original encoded bytes, never regenerated artwork.
            var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
            var store = new DataStore(Path.Combine(root,"desktop-logo")); var service = new TodoService(store,store.Load());
            service.SaveTask(new() { Description = "Keep task", ProjectId = service.Data.Projects[0].Id }); service.Change(d => d.BrandLogoPng = bytes);
            Assert(store.Load().BrandLogoPng.SequenceEqual(bytes)); var copy = Path.Combine(root,"logo.sumapp"); service.Export(copy);
            service.Change(d => d.BrandLogoPng = []); service.Restore(copy); Assert(service.Data.BrandLogoPng.SequenceEqual(bytes) && service.Data.Tasks.Single().Description == "Keep task");
            var before = File.ReadAllBytes(store.DataPath);
            var corrupt = bytes.ToArray(); corrupt[^15] ^= 1;
            foreach(var invalid in new[] { new byte[32], new byte[LogoAsset.MaxBytes+1], corrupt })
            { try { service.Change(d => d.BrandLogoPng = invalid); throw new Exception("Invalid PNG accepted"); } catch (ArgumentException) { } }
            Assert(File.ReadAllBytes(store.DataPath).SequenceEqual(before) && service.Data.BrandLogoPng.SequenceEqual(bytes));
            var old = System.Text.Json.Nodes.JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(service.Data,DataDocument.JsonOptions))!; old.AsObject().Remove("BrandLogoPng");
            Assert(System.Text.Json.JsonSerializer.Deserialize<DataDocument>(old.ToJsonString(),DataDocument.JsonOptions)!.BrandLogoPng.Length == 0);
        });
        check("A 1,000-node free hierarchy remains deterministic and separated during a collision move",() =>
        {
            var chart = Charts.Create("Large",ChartLayout.TopDown); for(var i = 1; i < 1000; i++) Charts.Add(chart,chart.Nodes[0].Id,true,"Node " + i);
            FreePlacement.Enable(chart); var original = ChartGeometry.Arrange(chart); var target = original.Boxes[2]; var timer = Stopwatch.StartNew();
            var result = FreePlacement.Move(original,original.Boxes[1].Id,target.X,target.Y); timer.Stop(); Separate(result);
            Console.WriteLine("  Free placement of 1,000 nodes: " + timer.ElapsedMilliseconds + " ms");
            Assert(result.Boxes.SequenceEqual(FreePlacement.Move(original,original.Boxes[1].Id,target.X,target.Y).Boxes));
        });
    }
}
