using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace EireTodo.Core;

public static class NetworkExports
{
    public const string XmlNamespace = "urn:eire:connection-diagram:1";
    public static byte[] Export(Diagram chart, ChartExportFormat format)
    {
        Charts.Validate(chart);
        if (chart.Kind != DiagramKind.Network) throw new ArgumentException("Select a connection diagram.");
        if (format == ChartExportFormat.NetworkXml)
        {
            XNamespace ns = XmlNamespace;
            var root = new XElement(ns + "Diagram", new XAttribute("id", chart.Id), new XAttribute("name", chart.Name), new XAttribute("version", 1), new XAttribute("zoom", chart.Zoom),
                chart.ProjectId.HasValue ? new XAttribute("projectId", chart.ProjectId) : null,
                new XElement(ns + "Nodes", chart.Nodes.Select(n => new XElement(ns + "Node", new XAttribute("id", n.Id), new XAttribute("number", n.Number), new XAttribute("x", n.X!), new XAttribute("y", n.Y!),
                    new XElement(ns + "Title", n.Title), new XElement(ns + "Notes", n.Notes), new XElement(ns + "Start", n.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), new XElement(ns + "Finish", n.FinishDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), new XElement(ns + "Completed", n.Completed), new XElement(ns + "DurationDays", n.DurationDays), new XElement(ns + "Format", JsonSerializer.Serialize(n.Format, DataDocument.JsonOptions))))),
                new XElement(ns + "Leads", chart.Leads.Select(l => new XElement(ns + "Lead", new XAttribute("id", l.Id), new XAttribute("from", l.From), new XAttribute("to", l.To), new XAttribute("doubleHeaded", l.DoubleHeaded), new XAttribute("routing", l.Routing), new XElement(ns + "Description", l.Description)))));
            using var stream = new MemoryStream(); new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(stream); return stream.ToArray();
        }
        if (format != ChartExportFormat.NetworkCsv) throw new ArgumentException("Use CSV / XML connection exports or PDF. WBS mode provides Microsoft Project and P6 planning exports.");
        var b = new StringBuilder();
        void Row(params object?[] cells) => b.Append(string.Join(",", cells.Select(v => "\"" + (Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Replace("\"", "\"\"") + "\""))).Append("\r\n");
        Row("Type", "ID", "Number", "Title / Description", "From Node ID", "To Node ID", "Double Headed", "Routing", "X", "Y", "Start", "Finish", "Completed", "Duration Days", "Notes", "Text Format JSON");
        foreach (var n in chart.Nodes.OrderBy(n => n.Order)) Row("Node", n.Id, n.Number, n.Title, "", "", "", "", n.X, n.Y, AustralianDates.Format(n.StartDate), AustralianDates.Format(n.FinishDate), n.Completed, n.DurationDays, n.Notes, JsonSerializer.Serialize(n.Format, DataDocument.JsonOptions));
        foreach (var l in chart.Leads) Row("Lead", l.Id, "", l.Description, l.From, l.To, l.DoubleHeaded, l.Routing, "", "", "", "", "", "", "", "");
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(b.ToString())).ToArray();
    }
}
