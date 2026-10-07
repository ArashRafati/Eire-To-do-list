using EireTodo.Core;
using System.Text.Json;

internal static class BrandChecks
{
    private static void Assert(bool value) { if (!value) throw new Exception("Branding / project assertion failed."); }
    private static void Reject(Action action) { try { action(); } catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException) { return; } throw new Exception("Expected rejection."); }
    public static void Run(Action<string, Action> check, string root)
    {
        check("Project deletion moves tasks and diagrams atomically and retains immutable task data", () =>
        {
            var store = new DataStore(Path.Combine(root, "project-delete")); var service = new TodoService(store, store.Load());
            var source = service.Data.Projects[0].Id; service.AddProject("Destination"); var target = service.Data.Projects.Single(p => p.Name == "Destination").Id;
            service.SaveTask(new() { ProjectId = source, Description = "Preserve", Notes = "full\nnotes", Completed = true, Priority = true });
            var task = service.Data.Tasks.Single(); var created = task.CreatedAt;
            var chart = Charts.Create("Linked diagram", ChartLayout.MindMap); chart.ProjectId = source; service.SaveDiagram(chart);
            Reject(() => service.DeleteProject(source)); Reject(() => service.DeleteProject(source, source));
            service.DeleteProject(source, target); var loaded = store.Load();
            Assert(!loaded.Projects.Any(p => p.Id == source) && loaded.Tasks.Single().ProjectId == target);
            Assert(loaded.Tasks.Single().Id == task.Id && loaded.Tasks.Single().CreatedAt == created && loaded.Tasks.Single().Notes == task.Notes && loaded.Tasks.Single().Completed && loaded.Tasks.Single().Priority);
            Assert(loaded.Diagrams.Single().Id == chart.Id && loaded.Diagrams.Single().ProjectId == target);
        });
        check("Deleting an empty project preserves diagrams and can create a destination for remaining tasks", () =>
        {
            var store = new DataStore(Path.Combine(root, "empty-project")); var s = new TodoService(store, store.Load()); var id = s.Data.Projects[0].Id;
            var diagram = Charts.Create("Keep diagram", ChartLayout.MindMap); diagram.ProjectId = id; s.SaveDiagram(diagram); s.DeleteProject(id);
            Assert(s.Data.Projects.Count == 0 && s.Data.Diagrams.Single().ProjectId is null);
            s.AddProject("New source"); id = s.Data.Projects[0].Id; s.SaveTask(new() { ProjectId = id, Description = "Keep task" });
            s.DeleteProject(id, newProjectName: "Moved tasks"); Assert(store.Load().Tasks.Single().ProjectId == store.Load().Projects.Single().Id);
            var before = File.ReadAllText(store.DataPath); Reject(() => s.DeleteProject(s.Data.Projects[0].Id, newProjectName: "  ")); Assert(File.ReadAllText(store.DataPath) == before);
        });
        check("Project deletion save failures preserve project, task assignments and the previous file", () =>
        {
            var store = new DataStore(Path.Combine(root, "delete-failure")); var s = new TodoService(store, store.Load()); var id = s.Data.Projects[0].Id;
            s.SaveTask(new() { ProjectId = id, Description = "Keep assignment" }); var before = File.ReadAllText(store.DataPath);
            var previous = store.DirectoryPath + "-preserved"; Directory.Move(store.DirectoryPath, previous); File.WriteAllText(store.DirectoryPath, "unavailable storage");
            Reject(() => s.DeleteProject(id, newProjectName: "Destination"));
            Assert(s.Data.Projects.Single().Id == id && s.Data.Tasks.Single().ProjectId == id && File.ReadAllText(Path.Combine(previous, "data.json")) == before);
        });
        check("Measured node dimensions adapt to glyph widths, wrapping, bold fonts and metadata", () =>
        {
            var node = new ChartNode { Title = "iiiiiiiiiiiiiiiiiiiiiiiiiiiiiiii" }; var narrow = ChartGeometry.Measure(node, 3);
            node.Title = "WWWWWWWWWWWWWWWWWWWWWWWWWWWWWWWW"; var wide = ChartGeometry.Measure(node, 3); Assert(wide.Height > narrow.Height);
            node.Title = "DN100 Stub Pipe vent from Drain Valve - stainless steel fabrication and supports, café Ω";
            var standard = ChartGeometry.Measure(node, 3); node.Format.Size = 48; node.Format.Bold = true; var large = ChartGeometry.Measure(node, 3);
            Assert(large.Width > standard.Width && large.Height > standard.Height);
            node.Notes = "Multiline\nfull notes"; Assert(ChartGeometry.Measure(node, 3).Height >= large.Height + 22);
        });
        check("Hierarchy layout uses renderer-supplied node measurements without clipping or overlaps", () =>
        {
            var chart = Charts.Create("Windows font measurement", ChartLayout.MindMap); var rootNode = chart.Nodes[0];
            for (var i = 0; i < 8; i++) Charts.Add(chart, rootNode.Id, true, "Sibling " + i);
            foreach (var layout in Enum.GetValues<ChartLayout>().Where(l => l != ChartLayout.Freeform))
            {
                chart.Layout = layout; var scene = ChartGeometry.Arrange(chart, measure: (n, level) => (300 + n.Order * 9, 160 + n.Order * 17));
                Assert(scene.Boxes.All(b => b.Height == 160 + chart.Nodes.Single(n => n.Id == b.Id).Order * 17));
                for (var i = 0; i < scene.Boxes.Count; i++) for (var j = i + 1; j < scene.Boxes.Count; j++) { var a = scene.Boxes[i]; var b = scene.Boxes[j]; Assert(a.X + a.Width <= b.X || b.X + b.Width <= a.X || a.Y + a.Height <= b.Y || b.Y + b.Height <= a.Y); }
            }
        });
        check("Default branding and all node states maintain readable text contrast", () =>
        {
            Assert(Contrast(BrandTheme.Ink, BrandTheme.Surface) >= 4.5 && Contrast(BrandTheme.White, BrandTheme.DarkTeal) >= 4.5);
            var chart = Charts.Create("SUMAPP", ChartLayout.MindMap); var node = chart.Nodes[0];
            foreach (var palette in Enum.GetValues<DiagramPalette>()) foreach (var level in new[] { 1, 2, 3 }) foreach (var priority in new[] { false, true }) foreach (var due in new[] { false, true })
            {
                chart.Palette = palette; node.Priority = priority; node.FinishDate = due ? DateOnly.FromDateTime(DateTime.Today.AddDays(-1)) : null;
                Assert(Contrast(DiagramAppearance.Text(chart, node, level), DiagramAppearance.Fill(chart, node, level)) >= 4.5);
            }
        });
        check("Priority flags survive restart, task editing, diagram backup and restore", () =>
        {
            var store = new DataStore(Path.Combine(root, "priority")); var s = new TodoService(store, store.Load());
            var task = new TodoTask { ProjectId = s.Data.Projects[0].Id, Description = "Priority task", Priority = true }; s.SaveTask(task);
            task.Description = "Edited priority"; s.SaveTask(task, task.Id);
            var chart = Charts.Create("Priority diagram", ChartLayout.MindMap); chart.Nodes[0].Priority = true; s.SaveDiagram(chart);
            var backup = Path.Combine(root, "priority-backup.json"); s.Export(backup); s.DeleteTask(task.Id); s.DeleteDiagram(chart.Id); s.Restore(backup);
            Assert(store.Load().Tasks.Single().Priority && store.Load().Diagrams.Single().Nodes[0].Priority);
        });
    }
    private static double Contrast(string a, string b)
    {
        static double L(string colour) { var bytes = Convert.FromHexString(colour[1..]); var c = bytes.Select(v => v / 255d).Select(v => v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4)).ToArray(); return .2126 * c[0] + .7152 * c[1] + .0722 * c[2]; }
        var first = L(a); var second = L(b); return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
    }
    public static void WriteFixtures(string directory)
    {
        Directory.CreateDirectory(directory);
        var chart = Charts.Create("SOU001 · Mechanical items", ChartLayout.MindMap);
        var fabrication = Charts.Add(chart, chart.Nodes[0].Id, true, "Fabrication");
        var procurement = Charts.Add(chart, chart.Nodes[0].Id, true, "Procurement"); procurement.Priority = true;
        foreach (var title in new[] { "Guide rail", "Chain hooks", "Chain supports", "Pipe supports", "DN100 Stub Pipe vent from Drain Valve — stainless steel fabrication and supports" }) Charts.Add(chart, fabrication.Id, true, title);
        Charts.Add(chart, procurement.Id, true, "Steel fabrication BOQ");
        var drawing = DiagramPdf.Compose(chart, new(IncludeDetails: false));
        File.WriteAllBytes(Path.Combine(directory, "SUMAPP-mind-map.pdf"), DiagramPdf.Write(drawing));
        var preview = new { Scene = ChartGeometry.Arrange(chart), Chart = chart, Marks = drawing.Pages[0].Layers[1].Marks.Select(m => new { Kind = m.GetType().Name, Data = (object)m }), Theme = new { BrandTheme.Primary, BrandTheme.DarkTeal, BrandTheme.Accent, BrandTheme.Ink, BrandTheme.Surface, BrandTheme.Yellow, BrandTheme.Black, BrandTheme.SoftTeal } };
        File.WriteAllText(Path.Combine(directory, "SUMAPP-design.json"), JsonSerializer.Serialize(preview, DataDocument.JsonOptions));
    }
}
