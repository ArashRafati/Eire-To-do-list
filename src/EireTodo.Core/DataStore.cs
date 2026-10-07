using System.Text;
using System.Text.Json;

namespace EireTodo.Core;

public sealed class DataStore
{
    public string DirectoryPath { get; }
    public string DataPath => Path.Combine(DirectoryPath, "data.json");
    public string RecoveryPath => Path.Combine(DirectoryPath, "data.previous.json");
    public DataStore(string directoryPath) { DirectoryPath = directoryPath; }
    public DataDocument Load() => File.Exists(DataPath) ? Read(DataPath) : new DataDocument();
    public static DataDocument Read(string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new IOException("The data file exceeds the 32 MB safety limit.");
        var data = JsonSerializer.Deserialize<DataDocument>(File.ReadAllText(path), DataDocument.JsonOptions)
            ?? throw new ArgumentException("The data file is empty.");
        Validation.Document(data);
        return data;
    }
    public void Save(DataDocument data)
    {
        Validation.Document(data);
        Directory.CreateDirectory(DirectoryPath);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, DataDocument.JsonOptions));
        if (bytes.Length > 32 * 1024 * 1024) throw new IOException("Data exceeds the 32 MB storage limit. Export a backup and reduce large notes before saving.");
        AtomicWrite(DataPath, bytes, File.Exists(DataPath) ? RecoveryPath : null);
    }
    public void Export(DataDocument data, string path)
    {
        Validation.Document(data);
        if (SamePath(path, DataPath) || SamePath(path, RecoveryPath))
            throw new ArgumentException("Export to another folder or filename, outside the live data files.");
        AtomicWrite(path, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, DataDocument.JsonOptions)), null);
    }
    public DataDocument Restore(string path)
    {
        var restored = Read(path); // Validate before touching the current document.
        if (File.Exists(DataPath))
        {
            var safety = Path.Combine(DirectoryPath, $"before-restore-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            File.Copy(DataPath, safety, false);
        }
        Save(restored);
        return restored;
    }
    public DataDocument RecoverPrevious()
    {
        var recovered = Read(RecoveryPath);
        if (File.Exists(DataPath))
            File.Move(DataPath, Path.Combine(DirectoryPath, $"unreadable-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json"));
        Save(recovered);
        return recovered;
    }
    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    internal static void WriteExport(string path, byte[] bytes) => AtomicWrite(path, bytes, null);

    private static void AtomicWrite(string path, byte[] bytes, string? backup)
    {
        var full = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(full)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(full)) File.Replace(temp, full, backup);
            else File.Move(temp, full);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}

public sealed partial class TodoService
{
    private readonly DataStore store;
    public DataDocument Data { get; private set; }
    public TodoService(DataStore store, DataDocument data) { this.store = store; Validation.Document(data); Data = data; }
    // Commit to disk first. A failed save leaves both the visible document and the previous on-disk state intact.
    public void Change(Action<DataDocument> change)
    {
        var next = Data.Clone();
        change(next);
        Validation.Document(next);
        Overdue.Sync(next, DateTimeOffset.Now);
        store.Save(next);
        Data = next;
    }
    public void SaveTask(TodoTask task, Guid? editingId = null)
    {
        var candidate = new TodoTask
        {
            Id = editingId ?? task.Id, ProjectId = task.ProjectId, Description = task.Description.Trim(),
            StartDate = task.StartDate, FinishDate = task.FinishDate, Category = task.Category.Trim(),
            Notes = task.Notes, CreatedAt = task.CreatedAt, Completed = task.Completed, Priority = task.Priority, Format = task.Format.Clone()
        };
        Validation.Task(candidate);
        Change(data =>
        {
            var project = data.Projects.SingleOrDefault(p => p.Id == candidate.ProjectId) ?? throw new ArgumentException("Choose a project.");
            if (editingId.HasValue)
            {
                var index = data.Tasks.FindIndex(t => t.Id == editingId);
                if (index < 0) throw new ArgumentException("This task no longer exists.");
                if (project.Archived && data.Tasks[index].ProjectId != project.Id) throw new ArgumentException("Choose an active project.");
                candidate.CreatedAt = data.Tasks[index].CreatedAt;
                data.Tasks[index] = candidate;
            }
            else
            {
                if (project.Archived) throw new ArgumentException("Choose an active project.");
                candidate.CreatedAt = DateTimeOffset.Now;
                data.Tasks.Add(candidate);
            }
            if (candidate.Category.Length > 0 && !data.Categories.Contains(candidate.Category, StringComparer.OrdinalIgnoreCase))
                data.Categories.Add(candidate.Category);
        });
    }
    public void AddProject(string name) => Change(d => d.Projects.Add(new Project { Name = name.Trim() }));
    public void RenameProject(Guid id, string name) => Change(d => d.Projects.Single(p => p.Id == id).Name = name.Trim());
    public void SetArchived(Guid id, bool archived) => Change(d => d.Projects.Single(p => p.Id == id).Archived = archived);
    public void DeleteProject(Guid id, Guid? destinationId = null, string? newProjectName = null) => Change(d =>
    {
        if (!d.Projects.Any(p => p.Id == id)) throw new ArgumentException("This project no longer exists.");
        var tasks = d.Tasks.Where(t => t.ProjectId == id).ToList();
        if (destinationId == id) throw new ArgumentException("Choose a different destination project.");
        if (destinationId.HasValue && newProjectName is not null) throw new ArgumentException("Choose one destination.");
        if (newProjectName is not null)
        {
            var destination = new Project { Name = newProjectName.Trim() };
            d.Projects.Add(destination); destinationId = destination.Id;
        }
        if (destinationId.HasValue && !d.Projects.Any(p => p.Id == destinationId && !p.Archived))
            throw new ArgumentException("Choose an active destination project.");
        if (tasks.Count > 0 && !destinationId.HasValue)
            throw new ArgumentException("Choose where to move this project's tasks before deleting it.");
        foreach (var task in tasks) task.ProjectId = destinationId!.Value;
        foreach (var diagram in d.Diagrams.Where(c => c.ProjectId == id)) diagram.ProjectId = destinationId;
        d.Projects.RemoveAll(p => p.Id == id);
    });
    public void SetCompleted(Guid id, bool completed) => Change(d => d.Tasks.Single(t => t.Id == id).Completed = completed);
    public void DeleteTask(Guid id) => Change(d => d.Tasks.RemoveAll(t => t.Id == id));
    public void SaveSettings(WindowSettings settings) => Change(d => d.Settings = settings);
    public void Restore(string path) => Data = store.Restore(path);
    public void Export(string path) => store.Export(Data, path);
}
