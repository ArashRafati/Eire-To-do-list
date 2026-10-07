namespace EireTodo.Core;

public enum OverdueItemKind { Task, Node }
public enum OverdueResolution { Active, Completed, Rescheduled, Deleted }
public sealed class OverdueEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public OverdueItemKind Kind { get; set; }
    public Guid ItemId { get; set; }
    public Guid? DiagramId { get; set; }
    public string Location { get; set; } = "";
    public string Title { get; set; } = "";
    public DateOnly DueDate { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public OverdueResolution Resolution { get; set; }
}

public static class Overdue
{
    public static bool IsDue(DateOnly? finish, bool completed, DateOnly? today = null) => !completed && finish.HasValue && finish < (today ?? DateOnly.FromDateTime(DateTime.Today));
    public static bool Sync(DataDocument data, DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        var items = data.Tasks.Select(t => (Kind: OverdueItemKind.Task, Id: t.Id, Diagram: (Guid?)null, Title: t.Description,
            Location: data.Projects.First(p => p.Id == t.ProjectId).Name, Due: t.FinishDate, Done: t.Completed)).Concat(
            data.Diagrams.SelectMany(d => d.Nodes.Select(n => (Kind: OverdueItemKind.Node, Id: n.Id, Diagram: (Guid?)d.Id, Title: n.Title, Location: d.Name, Due: n.FinishDate, Done: n.Completed)))).ToList();
        var changed = false;
        foreach (var entry in data.OverdueLog.Where(e => e.Resolution == OverdueResolution.Active))
        {
            var item = items.FirstOrDefault(i => i.Kind == entry.Kind && i.Id == entry.ItemId && i.Diagram == entry.DiagramId);
            var resolution = item.Id == Guid.Empty ? OverdueResolution.Deleted : item.Done ? OverdueResolution.Completed : item.Due != entry.DueDate || !IsDue(item.Due, item.Done, today) ? OverdueResolution.Rescheduled : OverdueResolution.Active;
            if (resolution != OverdueResolution.Active) { entry.Resolution = resolution; entry.ResolvedAt = now; changed = true; }
            else if (entry.Title != item.Title || entry.Location != item.Location) { entry.Title = item.Title; entry.Location = item.Location; changed = true; }
        }
        foreach (var item in items.Where(i => IsDue(i.Due, i.Done, today)))
        {
            if (data.OverdueLog.Any(e => e.Resolution == OverdueResolution.Active && e.Kind == item.Kind && e.ItemId == item.Id && e.DiagramId == item.Diagram)) continue;
            data.OverdueLog.Add(new() { Kind = item.Kind, ItemId = item.Id, DiagramId = item.Diagram, Title = item.Title, Location = item.Location, DueDate = item.Due!.Value, FirstSeen = now }); changed = true;
        }
        return changed;
    }
    public static void Validate(List<OverdueEntry> entries)
    {
        if (entries is null || entries.Any(e => e is null || e.Id == Guid.Empty || e.ItemId == Guid.Empty || e.Title is null || e.Location is null || e.FirstSeen == default || !Enum.IsDefined(e.Kind) || !Enum.IsDefined(e.Resolution) ||
            (e.Resolution == OverdueResolution.Active) != !e.ResolvedAt.HasValue || e.ResolvedAt < e.FirstSeen || e.Kind == OverdueItemKind.Node && !e.DiagramId.HasValue) || entries.Select(e => e.Id).Distinct().Count() != entries.Count ||
            entries.Where(e => e.Resolution == OverdueResolution.Active).GroupBy(e => (e.Kind, e.ItemId, e.DiagramId)).Any(g => g.Count() > 1))
            throw new ArgumentException("The overdue history is invalid.");
    }
}

public sealed partial class TodoService
{
    public bool RefreshOverdue(DateTimeOffset? now = null)
    {
        var next = Data.Clone(); if (!Overdue.Sync(next, now ?? DateTimeOffset.Now)) return false;
        store.Save(next); Data = next; return true;
    }
}
