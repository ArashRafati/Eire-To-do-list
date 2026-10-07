using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EireTodo.Core;

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "General";
    public bool Archived { get; set; }
    [JsonIgnore] public string DisplayName => Name + (Archived ? " (archived)" : "");
}

public sealed class TodoTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Description { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? FinishDate { get; set; }
    public string Category { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public bool Completed { get; set; }
    public bool Priority { get; set; }
    public TextFormat Format { get; set; } = new();
}

public sealed class WindowSettings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double Width { get; set; } = 880;
    public double Height { get; set; } = 530;
    public double Opacity { get; set; } = 0.97;
    public bool AlwaysOnTop { get; set; }
    public AppMode ActiveMode { get; set; }
    public Guid? SelectedDiagramId { get; set; }
    public Guid? SelectedNetworkId { get; set; }
    public bool ShowOverdueLog { get; set; }
    public Dictionary<string, double> ColumnWidths { get; set; } = [];
}

public sealed class DataDocument
{
    [JsonRequired] public int SchemaVersion { get; set; } = 1;
    [JsonRequired] public List<Project> Projects { get; set; } = [new()];
    [JsonRequired] public List<TodoTask> Tasks { get; set; } = [];
    [JsonRequired] public List<string> Categories { get; set; } = [];
    public List<Diagram> Diagrams { get; set; } = [];
    public List<OverdueEntry> OverdueLog { get; set; } = [];
    public byte[] BrandLogoPng { get; set; } = [];
    [JsonRequired] public WindowSettings Settings { get; set; } = new();
    public DataDocument Clone() => JsonSerializer.Deserialize<DataDocument>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
}

public static class AustralianDates
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-AU");
    public static string Format(DateOnly? value) => value?.ToString("dd/MM/yyyy", Culture) ?? "";
    public static string Format(DateTimeOffset value) => value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture);
    public static DateOnly? ParseOptional(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateOnly.TryParseExact(text.Trim(), "dd/MM/yyyy", Culture, DateTimeStyles.None, out var date))
            throw new ArgumentException("Enter dates as dd/MM/yyyy, for example 07/10/2026.");
        return date;
    }
    public static DateTime? ParseOptionalTime(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!DateTime.TryParseExact(text.Trim(), "dd/MM/yyyy HH:mm", Culture, DateTimeStyles.None, out var date))
            throw new ArgumentException("Enter created date/time as dd/MM/yyyy HH:mm (24-hour time).");
        return DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
    }
}

public static class Validation
{
    public static void Task(TodoTask task)
    {
        if (task.Format is null) throw new ArgumentException("Task formatting is missing.");
        task.Format.Validate();
        if (string.IsNullOrWhiteSpace(task.Description)) throw new ArgumentException("A task description is required.");
        if (task.StartDate.HasValue && task.FinishDate < task.StartDate)
            throw new ArgumentException("Finish date cannot be earlier than start date.");
        if (task.CreatedAt == default) throw new ArgumentException("A valid created date/time is required.");
    }
    public static void Document(DataDocument data)
    {
        LogoAsset.Validate(data.BrandLogoPng);
        if (data.SchemaVersion != 1) throw new ArgumentException("This backup uses an unsupported data format.");
        if (data.Projects is null || data.Tasks is null || data.Categories is null || data.Settings is null || data.Settings.ColumnWidths is null)
            throw new ArgumentException("The data file is incomplete.");
        if (data.Projects.Any(p => p is null || p.Id == Guid.Empty || string.IsNullOrWhiteSpace(p.Name)) ||
            data.Projects.Select(p => p.Id).Distinct().Count() != data.Projects.Count ||
            data.Projects.Select(p => p.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != data.Projects.Count)
            throw new ArgumentException("Projects must have unique names and IDs.");
        if (data.Tasks.Any(t => t is null || t.Id == Guid.Empty) || data.Tasks.Select(t => t.Id).Distinct().Count() != data.Tasks.Count)
            throw new ArgumentException("Task IDs must be unique.");
        foreach (var task in data.Tasks)
        {
            Task(task);
            if (!data.Projects.Any(p => p.Id == task.ProjectId)) throw new ArgumentException("A task refers to a missing project.");
            if (task.Category is null || task.Notes is null) throw new ArgumentException("A task has invalid optional text fields.");
        }
        if (data.Diagrams is null || data.Diagrams.Any(c => c is null) || data.Diagrams.Select(c => c.Id).Distinct().Count() != data.Diagrams.Count)
            throw new ArgumentException("Diagram IDs must be unique.");
        foreach (var chart in data.Diagrams)
        {
            Charts.Validate(chart);
            if (chart.ProjectId.HasValue && !data.Projects.Any(p => p.Id == chart.ProjectId)) throw new ArgumentException("A diagram refers to a missing project.");
        }
        Overdue.Validate(data.OverdueLog);
        if (!Enum.IsDefined(data.Settings.ActiveMode)) throw new ArgumentException("Invalid app mode.");
        if (data.Categories.Any(c => c is null)) throw new ArgumentException("A category is invalid.");
        var s = data.Settings;
        if (!double.IsFinite(s.Width) || !double.IsFinite(s.Height) || s.Width < 520 || s.Height < 340 ||
            !double.IsFinite(s.Opacity) || s.Opacity < .65 || s.Opacity > 1 ||
            s.Left.HasValue && !double.IsFinite(s.Left.Value) || s.Top.HasValue && !double.IsFinite(s.Top.Value) ||
            s.ColumnWidths.Values.Any(w => !double.IsFinite(w) || w < 35 || w > 3000))
            throw new ArgumentException("Window settings are invalid.");
    }
}
