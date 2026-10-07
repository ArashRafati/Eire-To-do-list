using System.Globalization;
using System.Text;
using System.Xml.Linq;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace EireTodo.Core;

public enum ChartExportFormat { HierarchyCsv, ProjectCsv, PrimaveraCsv, ProjectXml, PrimaveraXml, Pdf, NetworkCsv, NetworkXml }
public sealed record PlannedNode(OutlineNode Outline, DateOnly Start, DateOnly Finish, int Days);

public static class ChartExports
{
    public const string ProjectNamespace = "http://schemas.microsoft.com/project";
    public static readonly string[] PrimaveraVersions = ["18.8", "23.12", "24.12", "25.12"];
    public static List<PlannedNode> Plan(Diagram chart)
    {
        if (chart.Kind == DiagramKind.Network) throw new ArgumentException("Use a WBS diagram for planning exports.");
        var outline = Charts.Outline(chart); var planned = new Dictionary<Guid, PlannedNode>();
        foreach (var entry in outline.AsEnumerable().Reverse())
        {
            var n = entry.Node; DateOnly start, finish;
            if (entry.Summary)
            {
                var kids = Charts.Children(chart, n.Id).Select(c => planned[c.Id]).ToList();
                start = kids.Min(c => c.Start); finish = kids.Max(c => c.Finish);
            }
            else
            {
                start = n.StartDate ?? n.FinishDate?.AddDays(1 - n.DurationDays) ?? chart.ScheduleStart;
                finish = n.FinishDate ?? start.AddDays(n.DurationDays - 1);
            }
            planned[n.Id] = new(entry, start, finish, finish.DayNumber - start.DayNumber + 1);
        }
        return outline.Select(o => planned[o.Node.Id]).ToList();
    }
    public static byte[] Export(Diagram chart, ChartExportFormat format, string p6Version = "18.8")
    {
        Charts.Validate(chart);
        if (!Enum.IsDefined(format)) throw new ArgumentException("Unknown export format.");
        if (format == ChartExportFormat.Pdf) return Pdf(chart);
        if (chart.Kind == DiagramKind.Network) return NetworkExports.Export(chart, format);
        if (format is ChartExportFormat.NetworkCsv or ChartExportFormat.NetworkXml) throw new ArgumentException("Select a connection diagram for this export.");
        if (format == ChartExportFormat.ProjectXml) return XmlBytes(ProjectXml(chart));
        if (format == ChartExportFormat.PrimaveraXml) return XmlBytes(PrimaveraXml(chart, p6Version));
        return Csv(chart, format);
    }
    public static void Save(Diagram chart, ChartExportFormat format, string path, string p6Version = "18.8") => DataStore.WriteExport(path, Export(chart, format, p6Version));
    private static byte[] XmlBytes(XDocument document)
    {
        using var stream = new MemoryStream(); document.Save(stream); return stream.ToArray();
    }
    private static string Date(DateOnly date, bool finish = false) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + (finish ? "T17:00:00" : "T08:00:00");
    private static string CsvDate(DateOnly date, bool iso) => date.ToString(iso ? "yyyy-MM-dd" : "dd/MM/yyyy", CultureInfo.InvariantCulture);
    private static byte[] Csv(Diagram chart, ChartExportFormat format)
    {
        var b = new StringBuilder();
        void Row(params object?[] values) => b.AppendLine(string.Join(",", values.Select(v => "\"" + (Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Replace("\"", "\"\"") + "\"")));
        if (format == ChartExportFormat.ProjectCsv)
        {
            Row("Name", "Outline Level", "WBS", "Start", "Finish", "Duration", "% Complete", "Notes", "Unique ID", "Node ID", "Parent Node ID");
            foreach (var p in Plan(chart)) Row(p.Outline.Node.Title, p.Outline.Level, p.Outline.Code, CsvDate(p.Start, false), CsvDate(p.Finish, false), p.Days + "d", p.Outline.Node.Completed ? 100 : 0, p.Outline.Node.Notes, p.Outline.Node.Number, p.Outline.Node.Id, p.Outline.Node.ParentId);
        }
        else if (format == ChartExportFormat.PrimaveraCsv)
        {
            Row("Row Type", "Activity ID", "Activity Name", "WBS Code", "Parent WBS Code", "Original Duration (hours)", "Planned Start", "Planned Finish", "Activity Status", "Notebook", "Node ID", "Parent Node ID");
            var plan = Plan(chart); var codes = plan.ToDictionary(p => p.Outline.Node.Id, p => "EIRE." + p.Outline.Code);
            Row("WBS", "", chart.Name, "EIRE", "", "", "", "", "", "", chart.Id, "");
            foreach (var p in plan)
            {
                var n = p.Outline.Node; var parent = n.ParentId.HasValue ? codes[n.ParentId.Value] : "EIRE";
                Row(p.Outline.Summary ? "WBS" : "Activity", p.Outline.Summary ? "" : n.ActivityId, n.Title, p.Outline.Summary ? codes[n.Id] : parent, parent, p.Days * 8, CsvDate(p.Start, true), CsvDate(p.Finish, true), n.Completed ? "Completed" : "Not Started", n.Notes, n.Id, n.ParentId);
            }
        }
        else
        {
            Row("WBS", "Level", "Title", "Node ID", "Parent Node ID", "Permanent Number", "Type", "Start Date", "Finish Date", "Duration Days", "Completed", "Notes");
            foreach (var o in Charts.Outline(chart)) Row(o.Code, o.Level, o.Node.Title, o.Node.Id, o.Node.ParentId, o.Node.Number, o.Summary ? "WBS" : "Activity", AustralianDates.Format(o.Node.StartDate), AustralianDates.Format(o.Node.FinishDate), o.Node.DurationDays, o.Node.Completed, o.Node.Notes);
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(b.ToString().Replace("\r\n", "\n").Replace("\n", "\r\n"))).ToArray();
    }
    public static XDocument ProjectXml(Diagram chart)
    {
        XNamespace ns = ProjectNamespace; XElement E(string name, object? value) => new(ns + name, value);
        var plan = Plan(chart); var start = plan.Select(p => p.Start).DefaultIfEmpty(chart.ScheduleStart).Min(); var finish = plan.Select(p => p.Finish).DefaultIfEmpty(start).Max();
        XElement Task(int uid, int id, string name, string guid, string code, int level, DateOnly a, DateOnly z, int days, bool summary, bool completed, string notes) =>
            E("Task", new object[] { E("UID", uid), E("GUID", guid), E("ID", id), E("Name", name), E("Active", 1), E("Manual", 0), E("Type", 1), E("WBS", code), E("OutlineNumber", code), E("OutlineLevel", level), E("Start", Date(a)), E("Finish", Date(z, true)), E("Duration", $"PT{days * 8}H0M0S"), E("DurationFormat", 7), E("Estimated", 0), E("Milestone", 0), E("Summary", summary ? 1 : 0), E("PercentComplete", completed ? 100 : 0), E("CalendarUID", 1), E("Notes", notes) });
        var tasks = E("Tasks", Task(0, 0, chart.Name, chart.Id.ToString("D"), "0", 0, start, finish, finish.DayNumber - start.DayNumber + 1, true, false, "Exported from Eire. Undated leaf nodes use the diagram scheduling start. Seven-day, eight-hour calendar."));
        for (var i = 0; i < plan.Count; i++)
        {
            var p = plan[i]; var n = p.Outline.Node;
            tasks.Add(Task(n.Number, i + 1, n.Title, n.Id.ToString("D"), p.Outline.Code, p.Outline.Level, p.Start, p.Finish, p.Days, p.Outline.Summary, n.Completed, n.Notes));
        }
        var days = E("WeekDays", Enumerable.Range(1, 7).Select(d => E("WeekDay", new object[] { E("DayType", d), E("DayWorking", 1), E("WorkingTimes", new object[] { E("WorkingTime", new object[] { E("FromTime", "08:00:00"), E("ToTime", "12:00:00") }), E("WorkingTime", new object[] { E("FromTime", "13:00:00"), E("ToTime", "17:00:00") }) }) })));
        return new(new XDeclaration("1.0", "utf-8", null), E("Project", new object[] {
            E("SaveVersion", 14), E("UID", 1), E("Name", chart.Name), E("GUID", chart.Id.ToString("D")), E("Title", chart.Name), E("CreationDate", Date(chart.ScheduleStart)), E("ScheduleFromStart", 1), E("StartDate", Date(start)), E("FinishDate", Date(finish,true)), E("CalendarUID", 1), E("DefaultStartTime", "08:00:00"), E("DefaultFinishTime", "17:00:00"), E("MinutesPerDay", 480), E("MinutesPerWeek", 3360), E("DaysPerMonth", 30), E("Calendars", E("Calendar", new object[] { E("UID", 1), E("Name", "Eire 7 days - 8 hours"), E("IsBaseCalendar", 1), E("BaseCalendarUID", -1), days })), tasks }));
    }
    public static XDocument PrimaveraXml(Diagram chart, string version = "18.8")
    {
        if (!PrimaveraVersions.Contains(version)) throw new ArgumentException("Choose a supported Primavera XML version.");
        XNamespace ns = $"http://xmlns.oracle.com/Primavera/P6/V{version}/API/BusinessObjects";
        XElement E(string name, object? value) => new(ns + name, value);
        var plan = Plan(chart); var start = plan.Select(p => p.Start).DefaultIfEmpty(chart.ScheduleStart).Min();
        int WbsId(ChartNode n) => 10000000 + n.Number;
        int ActivityId(ChartNode n) => 20000000 + n.Number;
        var calendar = E("Calendar", new object[] { E("HoursPerDay",8), E("HoursPerMonth",240), E("HoursPerWeek",56), E("HoursPerYear",2920), E("IsDefault",false), E("Name","Eire 7 days - 8 hours"), E("ObjectId",1), E("ProjectObjectId",1), E("Type","Project"),
            E("StandardWorkWeek", new[] {"Sunday","Monday","Tuesday","Wednesday","Thursday","Friday","Saturday"}.Select(d => E("StandardWorkHours", new object[] { E("DayOfWeek",d), E("WorkTime",new object[] { E("Start","08:00:00"), E("Finish","11:59:00") }), E("WorkTime",new object[] { E("Start","13:00:00"), E("Finish","16:59:00") }) }))) });
        var project = E("Project", new object[] { E("ActivityDefaultActivityType", "Task Dependent"), E("ActivityDefaultCalendarObjectId", 1), E("ActivityDefaultDurationType", "Fixed Duration & Units"), E("ActivityDefaultPercentCompleteType", "Physical"), E("DataDate", Date(start)), E("GUID", chart.Id.ToString("B")), E("Id", "EIRE-" + chart.Id.ToString("N")[..8].ToUpperInvariant()), E("Name", chart.Name), E("ObjectId", 1), E("PlannedStartDate", Date(start)), E("Status", "Active"), calendar });
        project.Add(E("WBS", new object[] { E("Code", "EIRE"), E("Name", chart.Name), E("ObjectId", 10000000), E("ProjectObjectId", 1), E("SequenceNumber", 0), E("Status", "Active") }));
        foreach (var p in plan.Where(p => p.Outline.Summary))
        {
            var n = p.Outline.Node;
            project.Add(E("WBS", new object[] { E("Code", p.Outline.Code.Split('.').Last()), E("GUID", n.Id.ToString("B")), E("Name", n.Title), E("ObjectId", WbsId(n)), E("ParentObjectId", n.ParentId.HasValue ? WbsId(chart.Nodes.Single(c => c.Id == n.ParentId)) : 10000000), E("ProjectObjectId", 1), E("SequenceNumber", n.Order + 1), E("Status", "Active") }));
        }
        foreach (var p in plan.Where(p => !p.Outline.Summary))
        {
            var n = p.Outline.Node; var fields = new List<XElement>();
            if (n.Completed) { fields.Add(E("ActualDuration", p.Days * 8)); fields.Add(E("ActualFinishDate", Date(p.Finish, true))); fields.Add(E("ActualStartDate", Date(p.Start))); }
            fields.AddRange([E("AtCompletionDuration", p.Days * 8), E("CalendarObjectId", 1), E("DurationPercentComplete", n.Completed ? 1 : 0), E("DurationType", "Fixed Duration & Units"), E("FinishDate", Date(p.Finish, true)), E("GUID", n.Id.ToString("B")), E("Id", n.ActivityId), E("Name", n.Title), E("ObjectId", ActivityId(n)), E("PercentComplete", n.Completed ? 1 : 0), E("PercentCompleteType", "Physical"), E("PhysicalPercentComplete", n.Completed ? 1 : 0), E("PlannedDuration", p.Days * 8), E("PlannedFinishDate", Date(p.Finish, true)), E("PlannedStartDate", Date(p.Start)), E("ProjectObjectId", 1), E("RemainingDuration", n.Completed ? 0 : p.Days * 8), E("RemainingEarlyFinishDate", Date(p.Finish, true)), E("RemainingEarlyStartDate", Date(p.Start)), E("StartDate", Date(p.Start)), E("Status", n.Completed ? "Completed" : "Not Started"), E("Type", "Task Dependent"), E("WBSObjectId", n.ParentId.HasValue ? WbsId(chart.Nodes.Single(c => c.Id == n.ParentId)) : 10000000)]);
            project.Add(E("Activity", fields));
        }
        foreach (var p in plan.Where(p => !p.Outline.Summary && p.Outline.Node.Notes.Length > 0))
        {
            var n = p.Outline.Node;
            project.Add(E("ActivityNote", new object[] { E("ActivityObjectId", ActivityId(n)), E("Note", "<html><body><pre>" + System.Net.WebUtility.HtmlEncode(n.Notes) + "</pre></body></html>"), E("NotebookTopicObjectId", 1), E("ObjectId", 30000000 + n.Number), E("ProjectObjectId", 1) }));
        }
        // WBS notebook notes belong to ProjectNote; place them in schema order before Activity.
        var firstActivity = project.Elements(ns + "Activity").FirstOrDefault();
        foreach (var p in plan.Where(p => p.Outline.Summary && p.Outline.Node.Notes.Length > 0))
        {
            var n = p.Outline.Node; var note = E("ProjectNote", new object[] { E("Note", "<html><body><pre>" + System.Net.WebUtility.HtmlEncode(n.Notes) + "</pre></body></html>"), E("NotebookTopicObjectId", 1), E("ObjectId", 30000000 + n.Number), E("ProjectObjectId", 1), E("WBSObjectId", WbsId(n)) });
            if (firstActivity is null) project.Add(note); else firstActivity.AddBeforeSelf(note);
        }
        return new(new XDeclaration("1.0", "utf-8", null), E("APIBusinessObjects", new object[] { E("NotebookTopic", new object[] { E("AvailableForActivity", true), E("AvailableForEPS", false), E("AvailableForProject", true), E("AvailableForWBS", true), E("Name", "Eire notes"), E("ObjectId", 1), E("SequenceNumber", 1) }), project }));
    }

    public static byte[] Pdf(Diagram chart, PdfOptions? options = null) => DiagramPdf.Write(DiagramPdf.Compose(chart,options));
    private static readonly object FontLock = new();
    internal static void EnsureFonts() { lock (FontLock) { if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new EireFontResolver(); } }
    internal static IEnumerable<string> Wrap(XGraphics graphics, string text, XFont font, double width)
    {
        foreach (var paragraph in text.Replace("\r", "").Split('\n'))
        {
            var line = "";
            foreach (var rune in paragraph.EnumerateRunes())
            {
                var next = line + rune;
                if (graphics.MeasureString(next, font).Width > width && line.Length > 0) { yield return line; line = rune.ToString(); } else line = next;
            }
            yield return line;
        }
    }
    private sealed class EireFontResolver : IFontResolver
    {
        public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic) => familyName == "EireExport" ? new FontResolverInfo("EireDejaVu" + (bold ? "Bold" : "") + (italic ? "Italic" : "")) : null;
        public byte[]? GetFont(string faceName)
        {
            var file = faceName switch { "EireDejaVu" => "DejaVuSans.ttf", "EireDejaVuBold" => "DejaVuSans-Bold.ttf", "EireDejaVuItalic" => "DejaVuSans-Oblique.ttf", "EireDejaVuBoldItalic" => "DejaVuSans-BoldOblique.ttf", _ => null };
            if (file is null) return null;
            using var stream = typeof(ChartExports).Assembly.GetManifestResourceStream("EireTodo.Core.Assets." + file)!;
            using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray();
        }
    }
}
