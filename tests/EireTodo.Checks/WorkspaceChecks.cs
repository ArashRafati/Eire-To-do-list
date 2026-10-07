using System.Text.Json;
using EireTodo.Core;
using PdfSharp.Pdf.IO;

internal static class WorkspaceChecks
{
    private static void Assert(bool value) { if (!value) throw new Exception("Workspace assertion failed."); }
    internal static void Run(Action<string, Action> check, string directory)
    {
        check("Legacy module values open the combined hierarchy or graph without changing diagram layouts", () =>
        {
            Assert(AppModules.SelectorIndex((AppMode)1) == 1 && AppModules.SelectorIndex((AppMode)2) == 1 && AppModules.SelectorIndex((AppMode)3) == 2);
            Assert(AppModules.FromSelector(0) == AppMode.Todo && AppModules.FromSelector(1) == AppMode.Diagram && AppModules.FromSelector(2) == AppMode.Graph);
            var data = new DataDocument(); data.Settings.ActiveMode = (AppMode)2; var chart = Charts.Create("Old WBS", ChartLayout.TopDown); data.Diagrams.Add(chart); data.Settings.SelectedDiagramId = chart.Id;
            var store = new DataStore(Path.Combine(directory, "legacy-mode")); store.Save(data); var loaded = store.Load(); Assert(AppModules.Normalize(loaded.Settings.ActiveMode) == AppMode.Diagram && loaded.Diagrams.Single().Layout == ChartLayout.TopDown);
            loaded.Settings.ActiveMode = AppModules.FromSelector(2); store.Save(loaded); Assert(store.Load().Settings.ActiveMode == AppMode.Graph && store.Load().Diagrams.Single().Id == chart.Id);
        });
        check("An initial node is centred at each zoom with open workspace on all sides", () =>
        {
            foreach (var graph in new[] { false, true })
            {
                var diagram = graph ? NetworkCharts.Create("Graph") : Charts.Create("Diagram", ChartLayout.MindMap); var raw = ChartGeometry.Arrange(diagram); var bounds = OpenWorkspace.Bounds(diagram, raw);
                var origin = OpenWorkspace.Padding; var frame = OpenWorkspace.ToCanvas(raw, origin, origin);
                foreach (var zoom in new[] { .2, .8, 1d, 2d }) foreach (var viewport in new[] { (480d, 180d), (1100d, 600d) })
                {
                    var offset = OpenWorkspace.Centre(bounds, origin, origin, zoom, viewport.Item1, viewport.Item2); var node = frame.Boxes[0];
                    Assert(Math.Abs((node.X + node.Width / 2) * zoom - offset.X - viewport.Item1 / 2) < .001);
                    Assert(Math.Abs((node.Y + node.Height / 2) * zoom - offset.Y - viewport.Item2 / 2) < .001);
                    Assert(node.X > 3000 && node.Y > 3000 && frame.Width - node.X - node.Width > 3000 && frame.Height - node.Y - node.Height > 3000);
                }
            }
        });
        check("Both node creation paths link from the graph selection and allow incoming/double/unlinked overrides", () =>
        {
            var graph = NetworkCharts.Create("Automatic leads"); var root = graph.Nodes[0];
            var enter = GraphCreation.Add(graph, root.Id, "Enter node"); var insert = GraphCreation.Add(graph, enter.Id, "Insert node");
            Assert(graph.Leads.Any(l => l.From == root.Id && l.To == enter.Id && !l.DoubleHeaded)); Assert(graph.Leads.Any(l => l.From == enter.Id && l.To == insert.Id));
            var incoming = GraphCreation.Add(graph, root.Id, "Incoming", LeadDirection.Incoming, LeadRouting.SharpBends); var both = GraphCreation.Add(graph, insert.Id, "Both", LeadDirection.Both);
            Assert(graph.Leads.Any(l => l.From == incoming.Id && l.To == root.Id && l.Routing == LeadRouting.SharpBends)); Assert(graph.Leads.Any(l => l.From == insert.Id && l.To == both.Id && l.DoubleHeaded));
            var count = graph.Leads.Count; GraphCreation.Add(graph, root.Id, "Independent", unlinked: true); GraphCreation.Add(graph, null, "No selection"); Assert(graph.Leads.Count == count);
            var manual = GraphCreation.Connect(graph, root.Id, both.Id, LeadDirection.Incoming, LeadRouting.Curve); Assert(manual.From == both.Id && manual.To == root.Id);
        });
        check("Signed graph positions, leads and formatting survive restart, backup and viewport origin changes", () =>
        {
            var graph = NetworkCharts.Create("Free positions"); var root = graph.Nodes[0]; NetworkCharts.Move(graph, root.Id, -1800, -1200); var child = GraphCreation.Add(graph, root.Id); child.Format.Size = 32;
            var store = new DataStore(Path.Combine(directory, "signed-graph")); var service = new TodoService(store, store.Load()); service.SaveDiagram(graph); var backup = Path.Combine(directory, "signed-graph.json"); service.Export(backup);
            var loaded = store.Load().Diagrams.Single(); Assert(loaded.Nodes[0].X == -1800 && loaded.Nodes[0].Y == -1200 && loaded.Leads.Single().From == root.Id);
            var before = JsonSerializer.Serialize(loaded); var raw = ChartGeometry.Arrange(loaded);
            var bounds = OpenWorkspace.Bounds(loaded, raw); var originX = OpenWorkspace.Padding - bounds.X; var originY = OpenWorkspace.Padding - bounds.Y;
            foreach (var padding in new[] { 0d, OpenWorkspace.Padding, 3 * OpenWorkspace.Padding })
            {
                var frame = OpenWorkspace.ToCanvas(raw, originX + padding, originY + padding);
                foreach (var node in frame.Boxes) { var model = loaded.Nodes.Single(n => n.Id == node.Id); Assert(Math.Abs(node.X - originX - padding - model.X!.Value) < .001 && Math.Abs(node.Y - originY - padding - model.Y!.Value) < .001); }
            }
            Assert(JsonSerializer.Serialize(loaded) == before); service.DeleteDiagram(graph.Id); service.Restore(backup); Assert(service.Data.Diagrams.Single().Nodes[0].X == -1800);
        });
        check("Graph PDF compacts signed world positions without exporting the virtual workspace padding", () =>
        {
            var graph = NetworkCharts.Create("PDF free-space check"); var root = graph.Nodes[0]; NetworkCharts.Move(graph, root.Id, -5000, -3500); GraphCreation.Add(graph, root.Id, "Linked");
            var compact = OpenWorkspace.CompactGraph(graph); Assert(compact.Boxes.All(b => b.X >= 0 && b.Y >= 0 && b.X + b.Width <= compact.Width && b.Y + b.Height <= compact.Height)); Assert(compact.Width < 1500 && compact.Height < 1500);
            using var stream = new MemoryStream(ChartExports.Pdf(graph)); using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import); Assert(pdf.PageCount == 2);
        });
    }
}
