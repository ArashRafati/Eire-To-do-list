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
            foreach (var layout in Enum.GetValues<ChartLayout>())
            {
                chart.Layout = layout; var scene = ChartGeometry.Arrange(chart); Assert(scene.Boxes.Count == chart.Nodes.Count);
                Assert(scene.Boxes.All(b => b.X >= 0 && b.Y >= 0 && b.X + b.Width <= scene.Width && b.Y + b.Height <= scene.Height));
                foreach (var a in scene.Boxes) foreach (var b in scene.Boxes.Where(b => b.Id != a.Id)) Assert(a.X + a.Width <= b.X || b.X + b.Width <= a.X || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y);
            }
            chart.Nodes[0].Collapsed = true; Assert(ChartGeometry.Arrange(chart).Boxes.Count == 2); Assert(ChartGeometry.Arrange(chart, true).Boxes.Count == chart.Nodes.Count);
        });
        check("Diagrams, module, selection, zoom and node edits persist and restore", () =>
        {
            var store = new DataStore(Path.Combine(root, "charts")); var service = new TodoService(store, store.Load()); var chart = Fixture(); chart.ProjectId = service.Data.Projects[0].Id; chart.Zoom = .8;
            service.SaveDiagram(chart); service.Change(d => { d.Settings.ActiveMode = AppMode.Wbs; d.Settings.SelectedDiagramId = chart.Id; });
            var candidate = service.Data.Diagrams[0].Clone(); candidate.Nodes[2].Title = "Edited"; service.SaveDiagram(candidate);
            var restart = new TodoService(store, store.Load()); Assert(restart.Data.Diagrams[0].Nodes[2].Title == "Edited"); Assert(restart.Data.Diagrams[0].Nodes[2].Id == chart.Nodes[2].Id);
            Assert(restart.Data.Settings.ActiveMode == AppMode.Wbs && restart.Data.Settings.SelectedDiagramId == chart.Id && restart.Data.Diagrams[0].Zoom == .8);
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
    internal static void WriteFixtures(string directory)
    {
        Directory.CreateDirectory(directory); var chart = Fixture(); File.WriteAllText(Path.Combine(directory, "fixture.json"), JsonSerializer.Serialize(chart, DataDocument.JsonOptions));
        foreach (var format in Enum.GetValues<ChartExportFormat>()) ChartExports.Save(chart, format, Path.Combine(directory, format + (format == ChartExportFormat.Pdf ? ".pdf" : format is ChartExportFormat.ProjectXml or ChartExportFormat.PrimaveraXml ? ".xml" : ".csv")));
        foreach (var version in ChartExports.PrimaveraVersions) ChartExports.Save(chart, ChartExportFormat.PrimaveraXml, Path.Combine(directory, "Primavera-" + version + ".xml"), version);
    }
}
