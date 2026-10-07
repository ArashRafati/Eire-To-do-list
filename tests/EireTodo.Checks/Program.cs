using System.Text.Json;
using EireTodo.Core;

var failures = 0;
var passed = 0;
var root = Path.Combine(Path.GetTempPath(), "EireTodo-checks-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    Check("Australian dates reject ambiguous, invalid and reversed values", () =>
    {
        Assert(AustralianDates.ParseOptional("07/10/2026") == new DateOnly(2026, 10, 7));
        Assert(AustralianDates.Format(new DateOnly(2026, 10, 7)) == "07/10/2026");
        Assert(AustralianDates.ParseOptional("") is null);
        Throws(() => AustralianDates.ParseOptional("2026-10-07"));
        Throws(() => AustralianDates.ParseOptional("31/02/2026"));
        Throws(() => AustralianDates.ParseOptionalTime("07/10/2026 24:20"));
        Assert(AustralianDates.ParseOptionalTime("07/10/2026 18:20")!.Value.Hour == 18);
        Throws(() => Validation.Task(new TodoTask { Description = "Invalid", StartDate = new(2026, 10, 8), FinishDate = new(2026, 10, 7) }));
        Validation.Task(new TodoTask { Description = "Finish only", FinishDate = new(2026, 10, 7) });
    });
    Check("Create, edit and restart preserve immutable created time and reusable categories", () =>
    {
        var store = Store("editing"); var s = new TodoService(store, store.Load());
        var task = Task(s.Data.Projects[0].Id, "  First task  ");
        task.Category = "Work"; task.Notes = "line one\nline two";
        s.SaveTask(task);
        var first = s.Data.Tasks.Single(); var id = first.Id; var created = first.CreatedAt;
        var edit = new TodoTask { Description = "Edited", ProjectId = first.ProjectId, Category = "work", Notes = "All full notes", CreatedAt = created.AddDays(7), Completed = true };
        s.SaveTask(edit, id);
        Assert(s.Data.Tasks.Single().CreatedAt == created);
        Assert(s.Data.Tasks.Single().Id == id);
        Assert(s.Data.Categories.Count == 1);
        var restarted = new TodoService(new DataStore(store.DirectoryPath), new DataStore(store.DirectoryPath).Load());
        Assert(restarted.Data.Tasks.Single().Description == "Edited");
        Assert(restarted.Data.Tasks.Single().CreatedAt == created);
        Assert(restarted.Data.Tasks.Single().Notes == "All full notes");
        restarted.SetCompleted(id, false); Assert(!store.Load().Tasks.Single().Completed);
        restarted.SetCompleted(id, true); Assert(store.Load().Tasks.Single().Completed);
        restarted.DeleteTask(id); Assert(store.Load().Tasks.Count == 0);
        Assert(store.Load().Categories.Contains("Work"));
    });
    Check("Project create, rename, archive and unarchive preserve assigned tasks", () =>
    {
        var store = Store("projects"); var s = new TodoService(store, store.Load());
        s.AddProject("Client"); var id = s.Data.Projects.Single(p => p.Name == "Client").Id;
        s.SaveTask(Task(id, "Existing task"));
        s.RenameProject(id, "Client renamed"); s.SetArchived(id, true);
        Assert(s.Data.Tasks.Single().ProjectId == id);
        Assert(s.Data.Projects.Single(p => p.Id == id).Archived);
        Throws(() => s.SaveTask(Task(id, "Cannot assign to archive")));
        var edit = Task(id, "Edit in archived project"); s.SaveTask(edit, s.Data.Tasks[0].Id);
        Assert(s.Data.Tasks.Single().Description == "Edit in archived project");
        s.SetArchived(id, false); s.SaveTask(Task(id, "Allowed again"));
        Assert(store.Load().Tasks.Count == 2);
        Throws(() => s.AddProject(" client renamed "));
        Throws(() => s.RenameProject(id, " "));
        Assert(store.Load().Projects.Count == 2);
    });
    Check("Every column participates in combined filters", () =>
    {
        var project = Guid.NewGuid(); var other = Guid.NewGuid();
        var localTime = new DateTime(2026, 10, 7, 18, 20, 37, DateTimeKind.Local);
        var target = Task(project, "Review proposal"); target.Category = "Work"; target.Notes = "Send CLIENT feedback";
        target.StartDate = new(2026, 10, 7); target.FinishDate = new(2026, 10, 9); target.CreatedAt = new DateTimeOffset(localTime);
        var filter = new TaskFilter { ProjectId = project, TaskText = "PROPOSAL", NotesText = "client", CategoryMode = CategoryFilterMode.Value, Category = "work", Status = StatusFilter.ToDo,
            Start = new(DateFilterMode.Range, new(2026, 10, 7), new(2026, 10, 7)), Finish = new(DateFilterMode.Range, null, new(2026, 10, 9)),
            Created = new(new(2026, 10, 7, 18, 20, 0), new(2026, 10, 7, 18, 20, 0)) };
        filter.Validate(); Assert(filter.ActiveCount == 8); Assert(filter.Matches(target));
        var wrong = Task(other, "Review proposal"); Assert(!filter.Matches(wrong));
        target.Completed = true; Assert(!filter.Matches(target)); target.Completed = false;
        target.Notes = "irrelevant"; Assert(!filter.Matches(target)); target.Notes = "client";
        target.StartDate = new(2026, 10, 6); Assert(!filter.Matches(target)); target.StartDate = new(2026, 10, 7);
        target.FinishDate = new(2026, 10, 10); Assert(!filter.Matches(target)); target.FinishDate = new(2026, 10, 9);
        target.Category = "Personal"; Assert(!filter.Matches(target)); target.Category = "Work";
        target.CreatedAt = target.CreatedAt.AddMinutes(1); Assert(!filter.Matches(target));
        Assert(new TaskFilter().Matches(target));
    });
    Check("Blank fields, hide completed, open ranges and invalid ranges", () =>
    {
        var task = Task(Guid.NewGuid(), "Blank optional fields");
        var blanks = new TaskFilter { BlankNotes = true, CategoryMode = CategoryFilterMode.Blank, Start = new(DateFilterMode.Blank), Finish = new(DateFilterMode.Blank) };
        Assert(blanks.Matches(task)); task.Category = "X"; Assert(!blanks.Matches(task)); task.Category = "";
        task.Notes = "notes"; Assert(!blanks.Matches(task)); task.Notes = "";
        task.StartDate = new(2026, 10, 7); Assert(!blanks.Matches(task));
        Assert(new DateRange(DateFilterMode.Range, new(2026, 10, 7)).Matches(task.StartDate));
        Assert(!new DateRange(DateFilterMode.Range).Matches(null));
        task.Completed = true; Assert(!new TaskFilter { HideCompleted = true }.Matches(task));
        Assert(new TaskFilter { Status = StatusFilter.Completed }.Matches(task));
        Assert(!new TaskFilter { HideCompleted = true, Status = StatusFilter.Completed }.Matches(task));
        Throws(() => new DateRange(DateFilterMode.Range, new(2026, 10, 8), new(2026, 10, 7)).Validate());
        Throws(() => new CreatedRange(new(2026, 10, 8, 0, 0, 0), new(2026, 10, 7, 0, 0, 0)).Validate());
    });
    Check("All window settings and column widths survive restart and backup/restore", () =>
    {
        var store = Store("settings"); var s = new TodoService(store, store.Load());
        s.SaveTask(Task(s.Data.Projects[0].Id, "Backup task"));
        s.SaveSettings(new WindowSettings { Left = -1100, Top = 110, Width = 720, Height = 450, Opacity = .81, AlwaysOnTop = true, ColumnWidths = new() { ["Notes"] = 240 } });
        var settings = store.Load().Settings;
        Assert(settings.Left == -1100 && settings.Top == 110 && settings.Width == 720 && settings.Height == 450);
        Assert(settings.Opacity == .81 && settings.AlwaysOnTop && settings.ColumnWidths["Notes"] == 240);
        var backup = Path.Combine(root, "backup.json"); s.Export(backup);
        s.AddProject("Temporary"); s.DeleteTask(s.Data.Tasks[0].Id); s.Restore(backup);
        Assert(s.Data.Projects.Count == 1 && s.Data.Tasks.Single().Description == "Backup task");
        Assert(s.Data.Settings.AlwaysOnTop && s.Data.Settings.Opacity == .81);
        Assert(Directory.GetFiles(store.DirectoryPath, "before-restore-*.json").Length == 1);
        Throws(() => s.Export(store.DataPath));
    });
    Check("Invalid restore never overwrites good data; previous save enables recovery", () =>
    {
        var store = Store("recovery"); var s = new TodoService(store, store.Load());
        s.SaveTask(Task(s.Data.Projects[0].Id, "One")); s.AddProject("Two");
        var before = File.ReadAllText(store.DataPath);
        var invalid = Path.Combine(root, "invalid.json"); File.WriteAllText(invalid, "{bad json");
        Throws(() => s.Restore(invalid)); Assert(File.ReadAllText(store.DataPath) == before);
        var missing = s.Data.Clone(); missing.Tasks[0].ProjectId = Guid.NewGuid();
        File.WriteAllText(invalid, JsonSerializer.Serialize(missing, DataDocument.JsonOptions));
        Throws(() => s.Restore(invalid)); Assert(File.ReadAllText(store.DataPath) == before);
        missing = s.Data.Clone(); missing.SchemaVersion = 99;
        File.WriteAllText(invalid, JsonSerializer.Serialize(missing, DataDocument.JsonOptions));
        Throws(() => s.Restore(invalid)); Assert(File.ReadAllText(store.DataPath) == before);
        File.WriteAllText(invalid, "{}"); Throws(() => s.Restore(invalid));
        File.WriteAllText(store.DataPath, "corrupt"); Throws(() => store.Load());
        var recovered = store.RecoverPrevious();
        Assert(recovered.Tasks.Count == 1 && store.Load().Tasks.Count == 1);
        Assert(Directory.GetFiles(store.DirectoryPath, "unreadable-*.json").Length == 1);
    });
    Check("I/O and validation failures reject changes rather than losing task state", () =>
    {
        var store = Store("failures"); var s = new TodoService(store, store.Load());
        s.SaveTask(Task(s.Data.Projects[0].Id, "Keep me"));
        var previous = File.ReadAllText(store.DataPath);
        Throws(() => s.SaveTask(Task(s.Data.Projects[0].Id, "   ")));
        Assert(s.Data.Tasks.Single().Description == "Keep me" && File.ReadAllText(store.DataPath) == previous);
        var renamed = store.DirectoryPath + "-preserved";
        Directory.Move(store.DirectoryPath, renamed);
        File.WriteAllText(store.DirectoryPath, "Blocked directory simulates unavailable storage");
        Throws(() => s.SaveTask(Task(s.Data.Projects[0].Id, "Must not appear")));
        Assert(s.Data.Tasks.Single().Description == "Keep me");
        Assert(File.ReadAllText(Path.Combine(renamed, "data.json")) == previous);
        Assert(!Directory.GetFiles(renamed, "*.tmp").Any());
    });
    Check("A second session cannot open the same profile lock", () =>
    {
        var lockPath = Path.Combine(root, "session.lock");
        using var first = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        Throws(() => { using var second = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); });
    });
    ChartChecks.Run(Check, root);
    UpdateChecks.Run(Check, root);
    WorkspaceChecks.Run(Check, root);
    InteractionChecks.Run(Check, root);
    if (args.Length == 2 && args[0] == "--exports-dir") { ChartChecks.WriteFixtures(args[1]); UpdateChecks.WriteFixtures(args[1]); InteractionChecks.WriteFixtures(args[1]); }
}
finally { Directory.Delete(root, recursive: true); }
Console.WriteLine($"\n{passed} passed; {failures} failed. All checks executed as the current user.");
return failures == 0 ? 0 : 1;

void Check(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures++; Console.Error.WriteLine("FAIL " + name + ": " + e); }
}
DataStore Store(string name) => new(Path.Combine(root, name));
TodoTask Task(Guid project, string text) => new() { ProjectId = project, Description = text };
void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Throws(Action action)
{
    try { action(); }
    catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or JsonException) { return; }
    throw new Exception("Expected the operation to fail");
}
