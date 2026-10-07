using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using EireTodo.Core;
using PdfSharp.Pdf.IO;

internal static class UpdateChecks
{
    private static void Assert(bool condition) { if (!condition) throw new Exception("Update assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { return; } throw new Exception("Expected rejection."); }
    private static void NoOverlap(ChartScene scene)
    {
        for (var i = 0; i < scene.Boxes.Count; i++) for (var j = i + 1; j < scene.Boxes.Count; j++)
        {
            var a = scene.Boxes[i]; var b = scene.Boxes[j]; Assert(a.X + a.Width <= b.X || b.X + b.Width <= a.X || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y);
        }
    }
    internal static void Run(Action<string, Action> check, string directory)
    {
        check("Drag destinations preserve branch identity, relative order, codes and reject circular drops", () =>
        {
            var chart = Charts.Create("Drag", ChartLayout.MindMap); var root = chart.Nodes[0]; var a = Charts.Add(chart, root.Id, true, "A"); var b = Charts.Add(chart, a.Id, false, "B"); var c = Charts.Add(chart, b.Id, false, "C"); var child = Charts.Add(chart, b.Id, true, "Child");
            var ids = chart.Nodes.ToDictionary(n => n.Id, n => n.Number); var side = b.MindMapSide;
            Charts.MoveRelative(chart, b.Id, a.Id, NodeDrop.Before); Assert(Charts.Children(chart, root.Id).Select(n => n.Title).SequenceEqual(new[] { "B", "A", "C" })); Assert(b.MindMapSide == side);
            Charts.MoveRelative(chart, b.Id, c.Id, NodeDrop.After); Assert(Charts.Children(chart, root.Id).Select(n => n.Title).SequenceEqual(new[] { "A", "C", "B" }));
            a.Collapsed = true; Charts.MoveRelative(chart, b.Id, a.Id, NodeDrop.Child); Assert(b.ParentId == a.Id && !a.Collapsed && child.ParentId == b.Id);
            Assert(Charts.Outline(chart).Single(o => o.Node.Id == child.Id).Code == "1.1.1.1"); Assert(chart.Nodes.All(n => ids[n.Id] == n.Number));
            var before = JsonSerializer.Serialize(chart); Reject(() => Charts.MoveRelative(chart, a.Id, child.Id, NodeDrop.Before)); Reject(() => Charts.MoveRelative(chart, b.Id, b.Id, NodeDrop.Child)); Assert(JsonSerializer.Serialize(chart) == before);
            Charts.MoveRelative(chart, b.Id, root.Id, NodeDrop.After); Assert(b.ParentId is null && child.ParentId == b.Id); NoOverlap(ChartGeometry.Arrange(chart));
        });
        check("Mixed font sizes reserve non-overlapping space in all hierarchy layouts", () =>
        {
            var chart = ChartChecks.Fixture(); var parent = chart.Nodes.Last();
            for (var i = 0; i < 45; i++) { var n = Charts.Add(chart, i % 3 == 0 ? parent.Id : chart.Nodes[0].Id, true, "Formatted " + i); n.Format.Size = i % 2 == 0 ? 48 : 12; n.Format.Bold = true; n.Format.Alignment = TextJustification.Centre; }
            chart.Nodes[0].Format.Size = 36; Charts.Add(chart, null, false, "Other root").Format.Size = 44;
            foreach (var layout in Enum.GetValues<ChartLayout>().Where(l => l != ChartLayout.Freeform)) { chart.Layout = layout; var scene = ChartGeometry.Arrange(chart); NoOverlap(scene); Assert(scene.Boxes.Count == chart.Nodes.Count); Assert(scene.Boxes.Single(b => b.Id == chart.Nodes[1].Id).Height >= 118); }
            Reject(() => new TextFormat { Size = double.NaN }.Validate()); Reject(() => new TextFormat { Size = 49 }.Validate()); Reject(() => new TextFormat { Family = "file:///font" }.Validate());
        });
        check("New connection nodes auto-place without overlaps and preserve existing manual positions", () =>
        {
            var chart = NetworkCharts.Create("Network"); var root = chart.Nodes[0]; var positions = new Dictionary<Guid, (double?, double?)> { [root.Id] = (root.X, root.Y) };
            for (var i = 0; i < 75; i++) { var n = NetworkCharts.Add(chart, root.Id, true, "Node " + i, format: new TextFormat { Size = i % 4 == 0 ? 48 : 17 }); NoOverlap(ChartGeometry.Arrange(chart)); Assert(chart.Nodes.Where(x => positions.ContainsKey(x.Id)).All(x => positions[x.Id] == (x.X, x.Y))); positions[n.Id] = (n.X, n.Y); }
            var last = chart.Nodes.Last(); NetworkCharts.Move(chart, last.Id, 2500, 1700); Assert(last.X == 2500 && last.Y == 1700); NetworkCharts.Add(chart, last.Id, true); Assert(last.X == 2500 && last.Y == 1700);
            Reject(() => NetworkCharts.Move(chart, last.Id, double.NaN, 10));
        });
        check("Connection graphs support many incoming/outgoing, reverse and double leads, cycles and endpoint cleanup", () =>
        {
            var chart = NetworkCharts.Create("Leads"); var a = chart.Nodes[0]; var b = NetworkCharts.Add(chart, a.Id, false); var c = NetworkCharts.Add(chart, a.Id, false);
            NetworkCharts.Connect(chart, a.Id, b.Id); NetworkCharts.Connect(chart, a.Id, c.Id); NetworkCharts.Connect(chart, c.Id, b.Id, true, LeadRouting.SharpBends, "Both directions"); NetworkCharts.Connect(chart, b.Id, a.Id);
            Assert(chart.Leads.Count == 4 && chart.Leads.Count(l => l.From == a.Id) == 2 && chart.Leads.Count(l => l.To == b.Id) == 2); Charts.Validate(chart);
            var before = chart.Leads.Count; Reject(() => NetworkCharts.Connect(chart, a.Id, a.Id)); Reject(() => NetworkCharts.Connect(chart, Guid.NewGuid(), b.Id)); Assert(chart.Leads.Count == before);
            Reject(() => ChartExports.Export(chart, ChartExportFormat.ProjectXml)); Reject(() => ChartExports.Export(chart, ChartExportFormat.PrimaveraCsv));
            Charts.Delete(chart, b.Id); Assert(chart.Nodes.Count == 2 && chart.Leads.Count == 1 && chart.Leads[0].To == c.Id);
        });
        check("Lead routes keep arrows at cell edges and labels readable for curves and sharp bends", () =>
        {
            var a = new NodeBox(Guid.NewGuid(), 40, 40, 260, 118);
            foreach (var b in new[] { new NodeBox(Guid.NewGuid(), 450, 130, 300, 192), new NodeBox(Guid.NewGuid(), 50, 400, 260, 118), new NodeBox(Guid.NewGuid(), -400, 0, 260, 118), new NodeBox(Guid.NewGuid(), 10, -400, 260, 118) })
                foreach (var routing in Enum.GetValues<LeadRouting>())
                {
                    var p = LeadGeometry.Route(a, b, routing); Assert(p.LabelAngle >= -90 && p.LabelAngle <= 90 && double.IsFinite(p.LabelX));
                    Assert(p.X1 == a.X || p.X1 == a.X + a.Width || p.Y1 == a.Y || p.Y1 == a.Y + a.Height); Assert(p.X2 == b.X || p.X2 == b.X + b.Width || p.Y2 == b.Y || p.Y2 == b.Y + b.Height);
                    if (routing == LeadRouting.SharpBends) { Assert(p.X1 == p.C1X || p.Y1 == p.C1Y); Assert(p.C1X == p.C2X || p.C1Y == p.C2Y); Assert(p.C2X == p.X2 || p.C2Y == p.Y2); }
                }
        });
        check("Leads route around intervening cells in horizontal and vertical diagrams", () =>
        {
            var from = new NodeBox(Guid.NewGuid(), 40, 40, 260, 118);
            foreach (var vertical in new[] { false, true })
            {
                var middle = new NodeBox(Guid.NewGuid(), vertical ? 40 : 380, vertical ? 270 : 40, 260, 118);
                var to = new NodeBox(Guid.NewGuid(), vertical ? 40 : 720, vertical ? 500 : 40, 260, 118);
                foreach (var routing in Enum.GetValues<LeadRouting>())
                {
                    var r = LeadGeometry.Route(from, to, routing, obstacles: new[] { from, middle, to });
                    for (var i = 0; i <= 200; i++)
                    {
                        double t = i / 200d, x, y;
                        if (routing == LeadRouting.Curve) { var u = 1 - t; x = u*u*u*r.X1 + 3*u*u*t*r.C1X + 3*u*t*t*r.C2X + t*t*t*r.X2; y = u*u*u*r.Y1 + 3*u*u*t*r.C1Y + 3*u*t*t*r.C2Y + t*t*t*r.Y2; }
                        else { var points = new[] { (r.X1,r.Y1), (r.C1X,r.C1Y), (r.C2X,r.C2Y), (r.X2,r.Y2) }; var segment = Math.Min(2, (int)(t*3)); var f = t*3-segment; x = points[segment].Item1*(1-f)+points[segment+1].Item1*f; y = points[segment].Item2*(1-f)+points[segment+1].Item2*f; }
                        Assert(x <= middle.X || x >= middle.X+middle.Width || y <= middle.Y || y >= middle.Y+middle.Height);
                    }
                }
            }
        });
        check("Overdue episodes use local date boundaries and retain completion, rescheduling and deletion history", () =>
        {
            var data = new DataDocument(); var today = DateOnly.FromDateTime(DateTime.Today); var now = DateTimeOffset.Now;
            var t = new TodoTask { ProjectId = data.Projects[0].Id, Description = "Due yesterday", FinishDate = today.AddDays(-1) }; data.Tasks.Add(t);
            data.Tasks.Add(new() { ProjectId = t.ProjectId, Description = "Due today", FinishDate = today }); data.Tasks.Add(new() { ProjectId = t.ProjectId, Description = "Undated" });
            var chart = Charts.Create("Overdue WBS", ChartLayout.TopDown); chart.Nodes[0].FinishDate = today.AddDays(-2); data.Diagrams.Add(chart);
            Assert(Overdue.Sync(data, now)); Assert(data.OverdueLog.Count == 2); Assert(!Overdue.Sync(data, now.AddSeconds(1)));
            t.Completed = true; Overdue.Sync(data, now.AddMinutes(1)); Assert(data.OverdueLog.Single(e => e.ItemId == t.Id).Resolution == OverdueResolution.Completed);
            t.Completed = false; Overdue.Sync(data, now.AddMinutes(2)); Assert(data.OverdueLog.Count(e => e.ItemId == t.Id) == 2);
            t.FinishDate = today.AddDays(3); Overdue.Sync(data, now.AddMinutes(3)); Assert(data.OverdueLog.Last(e => e.ItemId == t.Id).Resolution == OverdueResolution.Rescheduled);
            data.Diagrams.Clear(); Overdue.Sync(data, now.AddMinutes(4)); Assert(data.OverdueLog.Single(e => e.Kind == OverdueItemKind.Node).Resolution == OverdueResolution.Deleted); Validation.Document(data);
        });
        check("Formatting, graph positions/leads, module and overdue history survive restart and all-data restore", () =>
        {
            var store = new DataStore(Path.Combine(directory, "update-persistence")); var service = new TodoService(store, store.Load()); var chart = GraphFixture(); service.SaveDiagram(chart);
            var task = new TodoTask { Description = "Formatted overdue", ProjectId = service.Data.Projects[0].Id, FinishDate = DateOnly.FromDateTime(DateTime.Today).AddDays(-1), Format = new() { Family = "Consolas", Size = 28, Bold = true, Italic = true, Underline = true, Alignment = TextJustification.Right } }; service.SaveTask(task);
            service.Change(d => { d.Settings.ActiveMode = AppMode.Graph; d.Settings.SelectedNetworkId = chart.Id; d.Settings.ShowOverdueLog = true; });
            var restart = store.Load(); Assert(restart.Tasks[0].Format.Size == 28 && restart.Tasks[0].Format.Italic && restart.Tasks[0].Format.Underline); Assert(restart.Diagrams[0].Leads.Count == chart.Leads.Count && restart.Diagrams[0].Nodes[1].X == chart.Nodes[1].X);
            Assert(restart.Settings.ShowOverdueLog && restart.Settings.ActiveMode == AppMode.Graph && restart.Settings.SelectedNetworkId == chart.Id && restart.OverdueLog.Count > 0);
            var backup = Path.Combine(directory, "update-backup.json"); service.Export(backup); service.DeleteTask(task.Id); service.DeleteDiagram(chart.Id); service.Restore(backup); Assert(JsonSerializer.Serialize(service.Data) == JsonSerializer.Serialize(restart));
            var legacy = JsonSerializer.SerializeToNode(new DataDocument(), DataDocument.JsonOptions)!.AsObject(); legacy.Remove("OverdueLog"); legacy.Remove("Diagrams"); var loaded = legacy.Deserialize<DataDocument>(DataDocument.JsonOptions)!; Validation.Document(loaded); Assert(loaded.OverdueLog.Count == 0);
        });
        check("Failed saves leave inline titles, formatting, graph moves and overdue history unchanged", () =>
        {
            var store = new DataStore(Path.Combine(directory, "update-failures")); var service = new TodoService(store, store.Load()); var chart = GraphFixture(); service.SaveDiagram(chart); var before = JsonSerializer.Serialize(service.Data);
            var preserved = store.DirectoryPath + "-preserved"; Directory.Move(store.DirectoryPath, preserved); File.WriteAllText(store.DirectoryPath, "Blocked storage");
            Reject(() => service.Change(d => { d.Diagrams[0].Nodes[0].Title = "Inline title"; d.Diagrams[0].Nodes[0].Format.Size = 36; NetworkCharts.Move(d.Diagrams[0], d.Diagrams[0].Nodes[1].Id, 500, 500); })); Assert(JsonSerializer.Serialize(service.Data) == before);
            Assert(JsonSerializer.Serialize(DataStore.Read(Path.Combine(preserved, "data.json"))) == before);
        });
        check("Network CSV/XML/PDF preserve Unicode labels, positions, directions, formats and full details", () =>
        {
            var chart = GraphFixture(); var csv = Encoding.UTF8.GetString(ChartExports.Export(chart, ChartExportFormat.NetworkCsv)); Assert(csv.Contains("From Node ID") && csv.Contains("Lead") && csv.Contains("café Ω") && csv.Contains("SharpBends"));
            using var xmlStream = new MemoryStream(ChartExports.Export(chart, ChartExportFormat.NetworkXml)); var xml = XDocument.Load(xmlStream); XNamespace ns = NetworkExports.XmlNamespace;
            Assert(xml.Root!.Element(ns + "Nodes")!.Elements().Count() == chart.Nodes.Count); var leads = xml.Root.Element(ns + "Leads")!.Elements().ToList(); Assert(leads.Count == chart.Leads.Count && leads.Any(l => (bool)l.Attribute("doubleHeaded")!));
            using var stream = new MemoryStream(ChartExports.Pdf(chart)); using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import); Assert(pdf.PageCount >= 2);
        });
    }
    internal static Diagram GraphFixture()
    {
        var chart = NetworkCharts.Create("Connection café Ω"); var a = chart.Nodes[0]; a.Format = new() { Size = 28, Bold = true, Italic = true, Underline = true, Alignment = TextJustification.Centre };
        var b = NetworkCharts.Add(chart, a.Id, false, "Review \"steel\", café Ω"); b.Notes = "First line, quoted\nSecond line Ω"; b.FinishDate = new(2026, 10, 6); b.Format.Alignment = TextJustification.Right;
        var c = NetworkCharts.Add(chart, b.Id, false, "Delivery"); NetworkCharts.Connect(chart, a.Id, b.Id, false, LeadRouting.Curve, "Design → review café Ω"); NetworkCharts.Connect(chart, b.Id, c.Id, true, LeadRouting.SharpBends, "Review ↔ delivery"); NetworkCharts.Connect(chart, c.Id, a.Id, false, LeadRouting.Curve, "Feedback cycle"); NetworkCharts.Move(chart, a.Id, -650, -420); NetworkCharts.Move(chart, b.Id, -150, -240); NetworkCharts.Move(chart, c.Id, 380, 100); return chart;
    }
    internal static void WriteFixtures(string directory)
    {
        var chart = GraphFixture(); foreach (var (format, ext) in new[] { (ChartExportFormat.NetworkCsv, "csv"), (ChartExportFormat.NetworkXml, "xml"), (ChartExportFormat.Pdf, "pdf") }) ChartExports.Save(chart, format, Path.Combine(directory, "network." + ext));
        File.WriteAllText(Path.Combine(directory, "network.json"), JsonSerializer.Serialize(chart, DataDocument.JsonOptions));
    }
}
