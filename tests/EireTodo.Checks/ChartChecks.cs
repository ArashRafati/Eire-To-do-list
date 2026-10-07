using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using EireTodo.Core;
using PdfSharp.Pdf.IO;

internal static class ChartChecks
{
    private static void Assert(bool value) { if (!value) throw new Exception("Chart assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { return; } throw new Exception("Expected chart operation rejection."); }
    internal static Diagram Fixture()
    {
        var chart = Charts.Create("Steel works & delivery", ChartLayout.TopDown); chart.ScheduleStart = new(2026, 10, 7);
        var root = chart.Nodes[0]; root.Notes = "Project notes"; var design = Charts.Add(chart, root.Id, true, "Design"); design.Notes = "Design WBS notes";
        var a = Charts.Add(chart, design.Id, true, "Review \"steel\", café Ω"); a.StartDate = new(2026, 10, 7); a.FinishDate = new(2026, 10, 9); a.DurationDays = 99; a.Notes = "First line, \"quoted\" & <text>\nSecond line: café Ω"; a.Completed = true;
        var b = Charts.Add(chart, design.Id, true, "Procurement"); b.DurationDays = 2;
        Charts.Add(chart, root.Id, true, "Fabrication").StartDate = new(2026, 10, 10);
        return chart;
    }
    internal static void Run(Action<string, Action> check, string root)
    {
        check("Sibling and child creation preserve hierarchy, order and permanent IDs", () =>
        {
            var chart = Charts.Create("Map", ChartLayout.MindMap); var parent = chart.Nodes[0]; var a = Charts.Add(chart, parent.Id, true, "A"); var b = Charts.Add(chart, a.Id, false, "B"); var child = Charts.Add(chart, a.Id, true, "Child"); var rootSibling = Charts.Add(chart, parent.Id, false, "Other root");
            Assert(b.ParentId == parent.Id && child.ParentId == a.Id && rootSibling.ParentId is null);
            Assert(Charts.Outline(chart).Select(o => o.Code).SequenceEqual(new[] { "1", "1.1", "1.1.1", "1.2", "2" }));
            var ids = chart.Nodes.ToDictionary(n => n.Id, n => n.Number); Charts.Reorder(chart, b.Id, -1); Assert(Charts.Children(chart, parent.Id)[0].Id == b.Id);
            Charts.Indent(chart, a.Id); Assert(a.ParentId == b.Id); Charts.Outdent(chart, a.Id); Assert(a.ParentId == parent.Id);
            Assert(chart.Nodes.All(n => ids[n.Id] == n.Number));
            Charts.Delete(chart, a.Id); Assert(!chart.Nodes.Any(n => n.Id == a.Id || n.Id == child.Id));
            Assert(Charts.Add(chart, parent.Id, true).Number > ids.Values.Max());
        });
        check("Invalid hierarchy, dates and depth reject chart commits", () =>
        {
            var chart = Fixture(); var child = chart.Nodes.First(n => n.ParentId is not null); Reject(() => Charts.Move(chart, chart.Nodes[0].Id, child.Id));
            var invalid = chart.Clone(); invalid.Nodes[0].ParentId = Guid.NewGuid(); Reject(() => Charts.Validate(invalid));
            invalid = chart.Clone(); invalid.Nodes[0].ParentId = child.Id; Reject(() => Charts.Validate(invalid));
            invalid = chart.Clone(); invalid.Nodes[1].Number = invalid.Nodes[0].Number; Reject(() => Charts.Validate(invalid));
            invalid = chart.Clone(); invalid.Nodes[1].StartDate = new(2026, 10, 8); invalid.Nodes[1].FinishDate = new(2026, 10, 7); Reject(() => Charts.Validate(invalid));
            invalid = chart.Clone(); invalid.Nodes[1].Title = " "; Reject(() => Charts.Validate(invalid));
            var deep = Charts.Create("Deep", ChartLayout.Outline); var p = deep.Nodes[0]; for (var i = 1; i < Charts.MaxDepth; i++) p = Charts.Add(deep, p.Id, true);
            Reject(() => Charts.Add(deep, p.Id, true));
        });
        check("Every layout places unequal subtrees without overlapping nodes", () =>
        {
            var chart = Fixture(); var selected = chart.Nodes.Last(); for (var i = 0; i < 8; i++) Charts.Add(chart, selected.Id, true, "Branch " + i);
            Charts.Add(chart, null, false, "Second root");
            foreach (var layout in Enum.GetValues<ChartLayout>().Where(l => l != ChartLayout.Freeform))
            {
                chart.Layout = layout; var scene = ChartGeometry.Arrange(chart); Assert(scene.Boxes.Count == chart.Nodes.Count);
                Assert(scene.Boxes.All(b => b.X >= 0 && b.Y >= 0 && b.X + b.Width <= scene.Width && b.Y + b.Height <= scene.Height));
                foreach (var a in scene.Boxes) foreach (var b in scene.Boxes.Where(b => b.Id != a.Id)) Assert(a.X + a.Width <= b.X || b.X + b.Width <= a.X || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y);
            }
            chart.Nodes[0].Collapsed = true; Assert(ChartGeometry.Arrange(chart).Boxes.Count == 2); Assert(ChartGeometry.Arrange(chart, true).Boxes.Count == chart.Nodes.Count);
        });
        check("New mind-map branches use free space and existing branches keep their side", () =>
        {
            var chart = Charts.Create("Dynamic", ChartLayout.MindMap); var rootNode = chart.Nodes[0];
            var heavy = Charts.Add(chart, rootNode.Id, true, "Heavy branch");
            for (var i = 0; i < 12; i++) Charts.Add(chart, heavy.Id, true, "Heavy child " + i);
            var opposite = Charts.Add(chart, heavy.Id, false, "Opposite branch");
            Assert(heavy.MindMapSide == MindMapBranchSide.Right && opposite.MindMapSide == MindMapBranchSide.Left);
            var inserted = Charts.Add(chart, heavy.Id, false, "Insert before existing sibling");
            Assert(inserted.MindMapSide == MindMapBranchSide.Left && opposite.MindMapSide == MindMapBranchSide.Left);
            var positions = ChartGeometry.Arrange(chart).Boxes.ToDictionary(b => b.Id);
            Assert(positions[heavy.Id].X > positions[rootNode.Id].X && positions[opposite.Id].X < positions[rootNode.Id].X);
            var previousHeight = ChartGeometry.Arrange(chart).Height;
            for (var i = 0; i < 20; i++) Charts.Add(chart, opposite.Id, true, "Growing left " + i);
            Assert(ChartGeometry.Arrange(chart).Height > previousHeight);
            Assert(heavy.MindMapSide == MindMapBranchSide.Right && opposite.MindMapSide == MindMapBranchSide.Left);
            var next = Charts.Add(chart, opposite.Id, false, "Next free side");
            Assert(next.MindMapSide == MindMapBranchSide.Right);
            var child = Charts.Add(chart, opposite.Id, true, "Child on parent's side");
            positions = ChartGeometry.Arrange(chart).Boxes.ToDictionary(b => b.Id);
            Assert(positions[child.Id].X < positions[opposite.Id].X);
            Charts.Reorder(chart, opposite.Id, -1); Assert(opposite.MindMapSide == MindMapBranchSide.Left);
            var side = inserted.MindMapSide; Charts.Delete(chart, next.Id); Assert(inserted.MindMapSide == side);
        });
        check("Branch side preferences survive restart, folding and chart backup/restore", () =>
        {
            var chart = Charts.Create("Side persistence", ChartLayout.MindMap); var rootNode = chart.Nodes[0];
            var a = Charts.Add(chart, rootNode.Id, true, "A"); var b = Charts.Add(chart, rootNode.Id, true, "B");
            for (var i = 0; i < 9; i++) Charts.Add(chart, a.Id, true);
            var sides = MindMapPlacement.ResolveSides(chart); a.Collapsed = true;
            Assert(MindMapPlacement.ResolveSides(chart).OrderBy(p => p.Key).SequenceEqual(sides.OrderBy(p => p.Key)));
            var store = new DataStore(Path.Combine(root, "branch-sides")); var service = new TodoService(store, store.Load());
            service.SaveDiagram(chart); var backup = Path.Combine(root, "branch-sides.json"); service.Export(backup);
            var restart = store.Load().Diagrams.Single(); Assert(restart.Nodes.Single(n => n.Id == a.Id).MindMapSide == a.MindMapSide);
            Assert(MindMapPlacement.ResolveSides(restart).OrderBy(p => p.Key).SequenceEqual(sides.OrderBy(p => p.Key)));
            service.DeleteDiagram(chart.Id); service.Restore(backup);
            Assert(service.Data.Diagrams.Single().Nodes.Single(n => n.Id == b.Id).MindMapSide == b.MindMapSide);
            var legacy = chart.Clone(); legacy.Nodes.ForEach(n => n.MindMapSide = MindMapBranchSide.Auto);
            var original = JsonSerializer.Serialize(legacy, DataDocument.JsonOptions); _ = ChartGeometry.Arrange(legacy);
            Assert(JsonSerializer.Serialize(legacy, DataDocument.JsonOptions) == original); // Drawing/export never changes saved data.
            service.SaveDiagram(legacy); Assert(service.Data.Diagrams.Single().Nodes.Where(n => n.ParentId == rootNode.Id).All(n => n.MindMapSide != MindMapBranchSide.Auto));
        });
        check("Rebalancing and parent changes account for whole subtree footprints", () =>
        {
            var chart = Charts.Create("Balance", ChartLayout.MindMap); var rootNode = chart.Nodes[0];
            var a = Charts.Add(chart, rootNode.Id, true, "A"); var b = Charts.Add(chart, rootNode.Id, true, "B"); var c = Charts.Add(chart, rootNode.Id, true, "C");
            foreach (var branch in new[] { a, b }) for (var i = 0; i < 10; i++) Charts.Add(chart, branch.Id, true);
            b.MindMapSide = a.MindMapSide;
            var height = ChartGeometry.Arrange(chart).Height; MindMapPlacement.Rebalance(chart);
            Assert(a.MindMapSide != b.MindMapSide && ChartGeometry.Arrange(chart).Height < height);
            var ids = chart.Nodes.ToDictionary(n => n.Id, n => n.Number); var moved = chart.Nodes.First(n => n.ParentId == a.Id);
            Charts.Move(chart, moved.Id, rootNode.Id); Assert(moved.MindMapSide != MindMapBranchSide.Auto);
            Assert(chart.Nodes.All(n => ids[n.Id] == n.Number));
            var invalid = chart.Clone(); invalid.Nodes[0].MindMapSide = (MindMapBranchSide)99; Reject(() => Charts.Validate(invalid));
        });
        check("Continuous sibling/child insertion stays deterministic and collision-free up to 1,000 nodes", () =>
        {
            var random = new Random(731); var chart = Charts.Create("Growing map", ChartLayout.MindMap);
            for (var i = 1; i < Charts.MaxNodes; i++)
            {
                var selected = chart.Nodes[random.Next(chart.Nodes.Count)];
                Charts.Add(chart, selected.Id, i % 3 != 0, "Node " + i);
                if (i % 25 == 0 || i == Charts.MaxNodes - 1)
                {
                    var scene = ChartGeometry.Arrange(chart); NoOverlap(scene); Assert(scene.Boxes.Count == chart.Nodes.Count);
                    Assert(scene == ChartGeometry.Arrange(chart) || scene.Boxes.SequenceEqual(ChartGeometry.Arrange(chart).Boxes));
                    foreach (var rootNode in chart.Nodes.Where(n => n.ParentId is null))
                    {
                        var index = scene.Boxes.ToDictionary(b => b.Id);
                        foreach (var branch in Charts.Children(chart, rootNode.Id))
                        {
                            var sign = branch.MindMapSide == MindMapBranchSide.Left ? -1 : 1;
                            foreach (var id in Charts.Descendants(chart, branch.Id)) Assert((index[id].X - index[rootNode.Id].X) * sign > 0);
                        }
                    }
                }
            }
            foreach (var layout in Enum.GetValues<ChartLayout>().Where(l => l != ChartLayout.Freeform)) { chart.Layout = layout; NoOverlap(ChartGeometry.Arrange(chart)); }
        });
        check("Diagrams, module, selection, zoom and node edits persist and restore", () =>
        {
            var store = new DataStore(Path.Combine(root, "charts")); var service = new TodoService(store, store.Load()); var chart = Fixture(); chart.ProjectId = service.Data.Projects[0].Id; chart.Zoom = .8;
            service.SaveDiagram(chart); service.Change(d => { d.Settings.ActiveMode = AppMode.LegacyWbs; d.Settings.SelectedDiagramId = chart.Id; });
            var candidate = service.Data.Diagrams[0].Clone(); candidate.Nodes[2].Title = "Edited"; service.SaveDiagram(candidate);
            var restart = new TodoService(store, store.Load()); Assert(restart.Data.Diagrams[0].Nodes[2].Title == "Edited"); Assert(restart.Data.Diagrams[0].Nodes[2].Id == chart.Nodes[2].Id);
            Assert(restart.Data.Settings.ActiveMode == AppMode.LegacyWbs && restart.Data.Settings.SelectedDiagramId == chart.Id && restart.Data.Diagrams[0].Zoom == .8);
            var backup = Path.Combine(root, "charts-backup.json"); restart.Export(backup); restart.DeleteDiagram(chart.Id); Assert(restart.Data.Diagrams.Count == 0); restart.Restore(backup); Assert(restart.Data.Diagrams.Single().Nodes.Count == chart.Nodes.Count);
            var json = JsonSerializer.Serialize(restart.Data, DataDocument.JsonOptions); using var old = JsonDocument.Parse(json); var fields = old.RootElement.EnumerateObject().Where(p => p.Name != "Diagrams").ToDictionary(p => p.Name, p => p.Value.Clone());
            var legacy = JsonSerializer.Deserialize<DataDocument>(JsonSerializer.Serialize(fields), DataDocument.JsonOptions)!; Validation.Document(legacy); Assert(legacy.Diagrams.Count == 0 && legacy.Tasks.Count == restart.Data.Tasks.Count);
        });
        check("Failed chart save and invalid restore leave saved diagrams intact", () =>
        {
            var store = new DataStore(Path.Combine(root, "chart-errors")); var service = new TodoService(store, store.Load()); service.SaveDiagram(Fixture()); var before = File.ReadAllText(store.DataPath);
            var candidate = service.Data.Diagrams[0].Clone(); candidate.Nodes[1].ParentId = Guid.NewGuid(); Reject(() => service.SaveDiagram(candidate)); Assert(File.ReadAllText(store.DataPath) == before);
            var bad = service.Data.Clone(); bad.Diagrams[0].Nodes[1].ParentId = Guid.NewGuid(); var path = Path.Combine(root, "bad-chart.json"); File.WriteAllText(path, JsonSerializer.Serialize(bad, DataDocument.JsonOptions)); Reject(() => service.Restore(path)); Assert(File.ReadAllText(store.DataPath) == before);
            var preserved = store.DirectoryPath + "-preserved"; Directory.Move(store.DirectoryPath, preserved); File.WriteAllText(store.DirectoryPath, "blocked storage"); candidate = service.Data.Diagrams[0].Clone(); candidate.Name = "Unsaved"; Reject(() => service.SaveDiagram(candidate)); Assert(service.Data.Diagrams[0].Name != "Unsaved"); Assert(File.ReadAllText(Path.Combine(preserved, "data.json")) == before);
        });
        check("Planning dates roll up, use fixed defaults, respect duration and finish-only dates", () =>
        {
            var chart = Fixture(); var plan = ChartExports.Plan(chart); var a = plan.Single(p => p.Outline.Node.Title.StartsWith("Review")); Assert(a.Days == 3 && a.Start == new DateOnly(2026, 10, 7) && a.Finish == new DateOnly(2026, 10, 9));
            var b = plan.Single(p => p.Outline.Node.Title == "Procurement"); Assert(b.Start == chart.ScheduleStart && b.Finish == new DateOnly(2026, 10, 8));
            Assert(plan[0].Start == new DateOnly(2026, 10, 7) && plan[0].Finish == new DateOnly(2026, 10, 10));
            var node = chart.Nodes.Single(n => n.Title == "Procurement"); node.FinishDate = new(2026, 10, 12); b = ChartExports.Plan(chart).Single(p => p.Outline.Node.Id == node.Id); Assert(b.Start == new DateOnly(2026, 10, 11) && b.Days == 2);
        });
        check("Microsoft Project XML preserves hierarchy, IDs, notes, dates and completion", () =>
        {
            var chart = Fixture(); chart.Nodes[0].Collapsed = true; XNamespace ns = ChartExports.ProjectNamespace; var doc = ChartExports.ProjectXml(chart); var tasks = doc.Root!.Element(ns + "Tasks")!.Elements(ns + "Task").ToList();
            Assert(tasks.Count == chart.Nodes.Count + 1); Assert(tasks[0].Element(ns + "UID")!.Value == "0");
            var exported = tasks.Single(t => t.Element(ns + "GUID")!.Value == chart.Nodes[2].Id.ToString("D")); Assert(exported.Element(ns + "Notes")!.Value == chart.Nodes[2].Notes); Assert(exported.Element(ns + "Duration")!.Value == "PT24H0M0S"); Assert(exported.Element(ns + "PercentComplete")!.Value == "100"); Assert(exported.Element(ns + "OutlineNumber")!.Value == "1.1.1");
            Assert(doc.Descendants(ns + "WeekDay").Count() == 7); using var xmlStream = new MemoryStream(ChartExports.Export(chart, ChartExportFormat.ProjectXml)); XDocument.Load(xmlStream);
        });
        check("Primavera XML carries WBS parent references, activities, notebooks and calendar", () =>
        {
            var chart = Fixture(); foreach (var version in ChartExports.PrimaveraVersions)
            {
                var doc = ChartExports.PrimaveraXml(chart, version); var ns = doc.Root!.Name.Namespace; var project = doc.Root.Element(ns + "Project")!; var wbs = project.Elements(ns + "WBS").ToList(); var activities = project.Elements(ns + "Activity").ToList();
                Assert(wbs.Count == 3 && activities.Count == 3); var ids = wbs.Select(w => w.Element(ns + "ObjectId")!.Value).ToHashSet(); Assert(activities.All(a => ids.Contains(a.Element(ns + "WBSObjectId")!.Value))); Assert(wbs.Skip(1).All(w => ids.Contains(w.Element(ns + "ParentObjectId")!.Value)));
                var activity = activities.Single(a => a.Element(ns + "GUID")!.Value == chart.Nodes[2].Id.ToString("B")); Assert(activity.Element(ns + "Id")!.Value == chart.Nodes[2].ActivityId); Assert(activity.Element(ns + "PlannedDuration")!.Value == "24"); Assert(activity.Element(ns + "ActualFinishDate") is not null); Assert(activity.Element(ns + "PhysicalPercentComplete")!.Value == "1");
                Assert(project.Elements(ns + "ActivityNote").Single().Element(ns + "Note")!.Value.Contains("&lt;text&gt;")); Assert(project.Elements(ns + "ProjectNote").Count() == 2); Assert(project.Descendants(ns + "StandardWorkHours").Count() == 7);
            }
            Reject(() => ChartExports.PrimaveraXml(chart, "99"));
        });
        check("CSV quoting and UTF-8 preserve multiline notes and planning mapping fields", () =>
        {
            var chart = Fixture(); foreach (var format in new[] { ChartExportFormat.HierarchyCsv, ChartExportFormat.ProjectCsv, ChartExportFormat.PrimaveraCsv })
            {
                var bytes = ChartExports.Export(chart, format); Assert(bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 })); var text = Encoding.UTF8.GetString(bytes); Assert(text.Contains("café Ω")); Assert(text.Contains("\"\"quoted\"\"")); Assert(text.Contains("Second line"));
                Assert(text.Contains(chart.Nodes[2].Id.ToString())); Assert(text.Contains("Node ID"));
            }
        });
        check("PDF opens with multiple chart/detail pages and an embedded Unicode font", () =>
        {
            var chart = Fixture(); for (var i = 0; i < 16; i++) Charts.Add(chart, chart.Nodes[0].Id, true, "Long branch " + i); chart.Nodes[0].Collapsed = true;
            var bytes = ChartExports.Export(chart, ChartExportFormat.Pdf); Assert(Encoding.ASCII.GetString(bytes.Take(5).ToArray()) == "%PDF-"); using var stream = new MemoryStream(bytes); using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import); Assert(pdf.PageCount >= 3); Assert(pdf.Info.Title == chart.Name);
            var text = Encoding.Latin1.GetString(bytes); Assert(text.Contains("/FontFile2") && text.Contains("/ToUnicode"));
            var path = Path.Combine(root, "chart.pdf"); ChartExports.Save(chart, ChartExportFormat.Pdf, path); Assert(File.ReadAllBytes(path).Take(5).SequenceEqual(bytes.Take(5)));
        });
    }
    private static void NoOverlap(ChartScene scene)
    {
        Assert(scene.Boxes.All(b => double.IsFinite(b.X) && double.IsFinite(b.Y) && b.X >= 0 && b.Y >= 0 && b.X + b.Width <= scene.Width && b.Y + b.Height <= scene.Height));
        var boxes = scene.Boxes.OrderBy(b => b.X).ToList();
        for (var i = 0; i < boxes.Count; i++) for (var j = i + 1; j < boxes.Count && boxes[j].X < boxes[i].X + boxes[i].Width; j++)
            Assert(boxes[i].Y + boxes[i].Height <= boxes[j].Y || boxes[j].Y + boxes[j].Height <= boxes[i].Y);
    }
    internal static void WriteFixtures(string directory)
    {
        Directory.CreateDirectory(directory); var chart = Fixture(); File.WriteAllText(Path.Combine(directory, "fixture.json"), JsonSerializer.Serialize(chart, DataDocument.JsonOptions));
        foreach (var format in Enum.GetValues<ChartExportFormat>().Where(f => f is not ChartExportFormat.NetworkCsv and not ChartExportFormat.NetworkXml)) ChartExports.Save(chart, format, Path.Combine(directory, format + (format == ChartExportFormat.Pdf ? ".pdf" : format is ChartExportFormat.ProjectXml or ChartExportFormat.PrimaveraXml ? ".xml" : ".csv")));
        foreach (var version in ChartExports.PrimaveraVersions) ChartExports.Save(chart, ChartExportFormat.PrimaveraXml, Path.Combine(directory, "Primavera-" + version + ".xml"), version);
    }
}
