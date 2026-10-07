namespace EireTodo.Core;

public static class AppModules
{
    // Preserve numeric values used by old backups; the former WBS mode now opens the combined module.
    public static AppMode Normalize(AppMode mode) => mode == AppMode.LegacyWbs ? AppMode.Diagram : mode;
    public static int SelectorIndex(AppMode mode) => Normalize(mode) switch { AppMode.Diagram => 1, AppMode.Graph => 2, _ => 0 };
    public static AppMode FromSelector(int index) => index switch { 1 => AppMode.Diagram, 2 => AppMode.Graph, _ => AppMode.Todo };
}

public sealed record SceneBounds(double X, double Y, double Width, double Height);
public static class OpenWorkspace
{
    public const double Padding = 4096;
    public static SceneBounds Bounds(Diagram chart, ChartScene scene)
    {
        if (scene.Boxes.Count == 0) return new(0, 0, 0, 0);
        var points = scene.Boxes.SelectMany(b => new[] { (b.X, b.Y), (b.X + b.Width, b.Y + b.Height) }).ToList();
        var boxes = scene.Boxes.ToDictionary(b => b.Id);
        foreach (var lead in chart.Leads)
        {
            var p = LeadGeometry.Route(boxes[lead.From], boxes[lead.To], lead.Routing, obstacles: scene.Boxes, fromSide: lead.FromSide, toSide: lead.ToSide);
            foreach (var point in LeadGeometry.Vertices(p)) points.Add((point.X, point.Y));
        }
        var x = points.Min(p => p.Item1); var y = points.Min(p => p.Item2);
        return new(x, y, points.Max(p => p.Item1) - x, points.Max(p => p.Item2) - y);
    }
    public static ChartScene ToCanvas(ChartScene scene, double originX, double originY)
    {
        var boxes = scene.Boxes.Select(b => b with { X = b.X + originX, Y = b.Y + originY }).ToList();
        return new(boxes, Math.Max(2 * Padding, scene.Width + originX + Padding), Math.Max(2 * Padding, scene.Height + originY + Padding));
    }
    public static (double X, double Y) Centre(SceneBounds bounds, double originX, double originY, double zoom, double viewportWidth, double viewportHeight)
        => (Math.Max(0, (originX + bounds.X + bounds.Width / 2) * zoom - viewportWidth / 2), Math.Max(0, (originY + bounds.Y + bounds.Height / 2) * zoom - viewportHeight / 2));
    public static ChartScene CompactGraph(Diagram chart)
    {
        var raw = ChartGeometry.Arrange(chart); var bounds = Bounds(chart, raw);
        var dx = ChartGeometry.Margin - bounds.X; var dy = ChartGeometry.Margin - bounds.Y;
        return new(raw.Boxes.Select(b => b with { X = b.X + dx, Y = b.Y + dy }).ToList(), bounds.Width + 2 * ChartGeometry.Margin, bounds.Height + 2 * ChartGeometry.Margin);
    }
}

public enum LeadDirection { Outgoing, Incoming, Both }
public static class GraphCreation
{
    public static ChartNode Add(Diagram chart, Guid? selected, string title = "New node", LeadDirection direction = LeadDirection.Outgoing, LeadRouting routing = LeadRouting.Curve, TextFormat? format = null, bool unlinked = false, Func<ChartNode, int, (double Width, double Height)>? measure = null)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentException("Choose a valid lead direction.");
        var node = NetworkCharts.Add(chart, selected, false, title, routing: routing, format: format, preferLeft: direction == LeadDirection.Incoming);
        if (selected is Guid source && !unlinked) Connect(chart, source, node.Id, direction, routing);
        // Direction can change the graph levels. Reserve the final renderer size after creating the lead.
        var boxes = ChartGeometry.Arrange(chart, measure: measure).Boxes;
        var cell = boxes.Single(b => b.Id == node.Id); var others = boxes.Where(b => b.Id != node.Id).ToList();
        bool Free(double x, double y) => others.All(b => x + cell.Width + 28 <= b.X || b.X + b.Width + 28 <= x || y + cell.Height + 28 <= b.Y || b.Y + b.Height + 28 <= y);
        var originalX = cell.X; var originalY = cell.Y; var found = Free(cell.X, cell.Y);
        for (var radius = 1; radius <= 64 && !found; radius++)
            for (var dy = -radius; dy <= radius && !found; dy++) for (var dx = -radius; dx <= radius && !found; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                var x = originalX + dx * (cell.Width + 74); var y = originalY + dy * (cell.Height + 74);
                if (Math.Abs(x) <= 100000 && Math.Abs(y) <= 100000 && Free(x, y)) { node.X = x; node.Y = y; found = true; }
            }
        if (!found) throw new ArgumentException("No free position was found. Move existing nodes to make space.");
        return node;
    }
    public static DiagramLead Connect(Diagram chart, Guid source, Guid target, LeadDirection direction, LeadRouting routing)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentException("Choose a valid lead direction.");
        return direction == LeadDirection.Incoming ? NetworkCharts.Connect(chart, target, source, routing: routing) : NetworkCharts.Connect(chart, source, target, direction == LeadDirection.Both, routing);
    }
}

public enum NodeEnterAction { Ignore, ConfirmTitle, CreateNode }
public static class NodeInputPolicy
{
    public static NodeEnterAction Enter(bool editingTitle, bool repeatedKey = false) => repeatedKey ? NodeEnterAction.Ignore : editingTitle ? NodeEnterAction.ConfirmTitle : NodeEnterAction.CreateNode;
}
