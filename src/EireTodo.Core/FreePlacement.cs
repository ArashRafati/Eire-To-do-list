namespace EireTodo.Core;

// Free hierarchy positioning never changes parent IDs, order, codes or task metadata.
public static class FreePlacement
{
    public const double Clearance = 18;
    public const double Limit = 100000;
    public static bool Intersects(NodeBox a, NodeBox b) => a.X < b.X + b.Width + Clearance - 1e-7 && a.X + a.Width + Clearance > b.X + 1e-7 && a.Y < b.Y + b.Height + Clearance - 1e-7 && a.Y + a.Height + Clearance > b.Y + 1e-7;

    public static ChartScene Arrange(Diagram chart, ChartScene automatic)
    {
        var original = automatic.Boxes.ToDictionary(b => b.Id);
        var nodes = chart.Nodes.ToDictionary(n => n.Id);
        var positioned = new Dictionary<Guid, NodeBox>();
        NodeBox Position(Guid id)
        {
            if (positioned.TryGetValue(id, out var existing)) return existing;
            var node = nodes[id]; var box = original[id];
            if (node.X is double x && node.Y is double y) box = box with { X = x, Y = y };
            else if (node.ParentId is Guid parent && original.TryGetValue(parent, out var before))
            {
                var after = Position(parent);
                box = box with { X = box.X + after.X - before.X, Y = box.Y + after.Y - before.Y };
            }
            positioned[id] = box; return box;
        }
        return Resolve(automatic.Boxes.Select(b => Position(b.Id)).ToList());
    }
    public static LeadPath Connection(NodeBox from, NodeBox to, ChartLayout layout) => LeadGeometry.Route(from,to,layout is ChartLayout.MindMap or ChartLayout.RightTree ? LeadRouting.Curve : LeadRouting.SharpBends);
    public static ChartScene Move(ChartScene original, Guid id, double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || Math.Abs(x) > Limit || Math.Abs(y) > Limit) throw new ArgumentException("Choose a finite position within the workspace bounds.");
        if (!original.Boxes.Any(b => b.Id == id)) throw new ArgumentException("The moved node is not visible.");
        var pinned = original.Boxes.Single(b => b.Id == id) with { X = x, Y = y };
        return Resolve(original.Boxes, pinned);
    }
    private static ChartScene Resolve(IReadOnlyList<NodeBox> original, NodeBox? pinned = null)
    {
        var settled = new List<NodeBox>(original.Count);
        if (pinned is not null) settled.Add(pinned);
        foreach (var box in original.Where(b => b.Id != pinned?.Id))
        {
            var desired = box with { X = Math.Clamp(box.X, -Limit, Limit), Y = Math.Clamp(box.Y, -Limit, Limit) };
            var queue = new PriorityQueue<NodeBox, (double Distance, int Order)>();
            var seen = new HashSet<(double, double)>(); var sequence = 0;
            void Candidate(double x, double y)
            {
                if (Math.Abs(x) > Limit || Math.Abs(y) > Limit || !seen.Add((x,y))) return;
                queue.Enqueue(desired with { X = x, Y = y }, ((x-desired.X)*(x-desired.X)+(y-desired.Y)*(y-desired.Y), sequence++));
            }
            Candidate(desired.X, desired.Y); NodeBox? chosen = null;
            while (queue.TryDequeue(out var candidate, out _) && seen.Count <= Math.Max(128, settled.Count * 32))
            {
                var obstacle = settled.FirstOrDefault(other => Intersects(candidate, other));
                if (obstacle is null) { chosen = candidate; break; }
                Candidate(obstacle.X - desired.Width - Clearance, candidate.Y);
                Candidate(obstacle.X + obstacle.Width + Clearance, candidate.Y);
                Candidate(candidate.X, obstacle.Y - desired.Height - Clearance);
                Candidate(candidate.X, obstacle.Y + obstacle.Height + Clearance);
            }
            if (chosen is null) throw new ArgumentException("No free space was found near this position. Move the node into a clearer area.");
            settled.Add(chosen);
        }
        var lookup = settled.ToDictionary(b => b.Id);
        var result = original.Select(b => lookup[b.Id]).ToList();
        return new(result, Math.Max(500, result.Select(b => b.X + b.Width + ChartGeometry.Margin).DefaultIfEmpty(0).Max()), Math.Max(300, result.Select(b => b.Y + b.Height + ChartGeometry.Margin).DefaultIfEmpty(0).Max()));
    }
    public static void Commit(Diagram chart, ChartScene scene)
    {
        if (chart.Kind != DiagramKind.Hierarchy) throw new ArgumentException("Use hierarchy placement for mind maps and WBS diagrams.");
        var nodes = chart.Nodes.ToDictionary(n => n.Id);
        foreach (var box in scene.Boxes)
        {
            if (!nodes.ContainsKey(box.Id) || !double.IsFinite(box.X) || !double.IsFinite(box.Y) || Math.Abs(box.X) > Limit || Math.Abs(box.Y) > Limit) throw new ArgumentException("Invalid node position.");
        }
        foreach (var box in scene.Boxes) { nodes[box.Id].X = box.X; nodes[box.Id].Y = box.Y; }
    }
    public static void Enable(Diagram chart, Func<ChartNode,int,(double Width,double Height)>? measure = null)
    {
        if (chart.Kind != DiagramKind.Hierarchy) throw new ArgumentException("Graphs already allow free movement.");
        var next = chart.Clone(); next.FreeMove = true;
        var scene = ChartGeometry.Arrange(next, measure: measure); Commit(next, scene);
        chart.FreeMove = true;
        foreach (var node in chart.Nodes) { var placed = next.Nodes.Single(n => n.Id == node.Id); node.X = placed.X; node.Y = placed.Y; }
    }
    public static void Reset(Diagram chart)
    {
        if (chart.Kind != DiagramKind.Hierarchy) throw new ArgumentException("Choose a hierarchy diagram.");
        foreach (var node in chart.Nodes) { node.X = null; node.Y = null; }
    }
}
