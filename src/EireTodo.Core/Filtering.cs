namespace EireTodo.Core;

public enum DateFilterMode { Any, Range, Blank }
public enum StatusFilter { Any, ToDo, Completed }
public enum CategoryFilterMode { Any, Value, Blank }

public sealed record DateRange(DateFilterMode Mode = DateFilterMode.Any, DateOnly? From = null, DateOnly? To = null)
{
    public bool Matches(DateOnly? value) => Mode switch
    {
        DateFilterMode.Any => true,
        DateFilterMode.Blank => value is null,
        _ => value.HasValue && (!From.HasValue || value >= From) && (!To.HasValue || value <= To)
    };
    public void Validate()
    {
        if (From.HasValue && To < From) throw new ArgumentException("Filter end date cannot be earlier than its start date.");
    }
}

public sealed record CreatedRange(DateTime? From = null, DateTime? To = null)
{
    public bool Matches(DateTimeOffset value)
    {
        var local = value.LocalDateTime;
        // Upper bound includes the whole selected minute, matching the displayed precision.
        return (!From.HasValue || local >= From) && (!To.HasValue || local.Ticks / TimeSpan.TicksPerMinute <= To.Value.Ticks / TimeSpan.TicksPerMinute);
    }
    public void Validate()
    {
        if (From.HasValue && To < From) throw new ArgumentException("Created filter end cannot be earlier than its start.");
    }
}

public sealed class TaskFilter
{
    public Guid? ProjectId { get; init; }
    public string TaskText { get; init; } = "";
    public string NotesText { get; init; } = "";
    public bool BlankNotes { get; init; }
    public CategoryFilterMode CategoryMode { get; init; }
    public string Category { get; init; } = "";
    public StatusFilter Status { get; init; }
    public bool HideCompleted { get; init; }
    public DateRange Start { get; init; } = new();
    public DateRange Finish { get; init; } = new();
    public CreatedRange Created { get; init; } = new();
    public int ActiveCount => (ProjectId.HasValue ? 1 : 0) + (TaskText.Length > 0 ? 1 : 0) +
        (NotesText.Length > 0 || BlankNotes ? 1 : 0) + (CategoryMode != CategoryFilterMode.Any ? 1 : 0) +
        (Status != StatusFilter.Any || HideCompleted ? 1 : 0) + (Start.Mode != DateFilterMode.Any ? 1 : 0) +
        (Finish.Mode != DateFilterMode.Any ? 1 : 0) + (Created.From.HasValue || Created.To.HasValue ? 1 : 0);
    public void Validate() { Start.Validate(); Finish.Validate(); Created.Validate(); }
    public bool Matches(TodoTask task) =>
        (!ProjectId.HasValue || task.ProjectId == ProjectId) &&
        task.Description.Contains(TaskText, StringComparison.OrdinalIgnoreCase) &&
        task.Notes.Contains(NotesText, StringComparison.OrdinalIgnoreCase) &&
        (!BlankNotes || string.IsNullOrWhiteSpace(task.Notes)) &&
        (CategoryMode == CategoryFilterMode.Any || CategoryMode == CategoryFilterMode.Blank && string.IsNullOrWhiteSpace(task.Category) ||
            CategoryMode == CategoryFilterMode.Value && string.Equals(task.Category, Category, StringComparison.OrdinalIgnoreCase)) &&
        (Status == StatusFilter.Any || task.Completed == (Status == StatusFilter.Completed)) &&
        (!HideCompleted || !task.Completed) && Start.Matches(task.StartDate) && Finish.Matches(task.FinishDate) && Created.Matches(task.CreatedAt);
}
