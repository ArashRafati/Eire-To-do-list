using System.Text.Json;
using System.Text.Json.Serialization;

namespace EireTodo.Core;

public enum AppMode { Todo = 0, Diagram = 1, LegacyWbs = 2, Graph = 3 }
public enum ChartLayout { MindMap, RightTree, TopDown, LeftToRight, Outline, Freeform }
public enum MindMapBranchSide { Auto, Right, Left }

public sealed class ChartNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public Guid? ParentId { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = "New node";
    public bool Priority { get; set; }
    public string Notes { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? FinishDate { get; set; }
    public int DurationDays { get; set; } = 1;
    public bool Completed { get; set; }
    public bool Collapsed { get; set; }
    public MindMapBranchSide MindMapSide { get; set; }
    public TextFormat Format { get; set; } = new();
    public double? X { get; set; }
    public double? Y { get; set; }
    [JsonIgnore] public string ActivityId => $"A{Number:D5}";
}

public sealed class Diagram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New diagram";
    public Guid? ProjectId { get; set; }
    public DiagramKind Kind { get; set; }
    public List<DiagramLead> Leads { get; set; } = [];
    public ChartLayout Layout { get; set; } = ChartLayout.MindMap;
    public DiagramPalette Palette { get; set; }
    public NodeDesign Design { get; set; }
    public DateOnly ScheduleStart { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public double Zoom { get; set; } = 1;
    public int NextNumber { get; set; } = 1;
    public List<ChartNode> Nodes { get; set; } = [];
    public Diagram Clone() => JsonSerializer.Deserialize<Diagram>(JsonSerializer.Serialize(this, DataDocument.JsonOptions), DataDocument.JsonOptions)!;
}

public sealed record OutlineNode(ChartNode Node, string Code, int Level, bool Summary);

public static class Charts
{
    public const int MaxNodes = 1000;
    public const int MaxDepth = 32;
    public static string LayoutName(ChartLayout layout) => layout switch
    {
        ChartLayout.MindMap => "Mind map · two sides",
        ChartLayout.RightTree => "Mind map · right tree",
        ChartLayout.TopDown => "WBS · top down",
        ChartLayout.LeftToRight => "WBS · left to right",
        ChartLayout.Freeform => "Graph · free placement",
        _ => "Numbered outline"
    };
    public static Diagram Create(string name, ChartLayout layout, Guid? projectId = null)
    {
        var chart = new Diagram { Name = name.Trim(), Layout = layout, ProjectId = projectId };
        Add(chart, null, false, name.Trim()); return chart;
    }
    public static void Validate(Diagram chart)
    {
        if (chart is null || chart.Id == Guid.Empty || string.IsNullOrWhiteSpace(chart.Name) || chart.Name.Length > 100 ||
            !Enum.IsDefined(chart.Layout) || !Enum.IsDefined(chart.Palette) || !Enum.IsDefined(chart.Design) || chart.Nodes is null || chart.Nodes.Count > MaxNodes ||
            chart.ScheduleStart.Year < 1900 || chart.ScheduleStart.Year > 2090 || !double.IsFinite(chart.Zoom) || chart.Zoom < .2 || chart.Zoom > 2)
            throw new ArgumentException("Invalid diagram: use a name up to 100 characters and no more than 1,000 nodes.");
        if (chart.Nodes.Any(n => n is null || n.Id == Guid.Empty || n.Number < 1 || n.Number > 1000000 || n.Order < 0 ||
            string.IsNullOrWhiteSpace(n.Title) || n.Title.Length > 100 || n.Notes is null || n.Notes.Length > 250000 ||
            !Enum.IsDefined(n.MindMapSide) || n.DurationDays < 1 || n.DurationDays > 3650 || n.StartDate?.Year < 1900 || n.FinishDate?.Year < 1900 ||
            n.StartDate?.Year > 2090 || n.FinishDate?.Year > 2100 || n.StartDate.HasValue && n.FinishDate < n.StartDate) ||
            chart.Nodes.Select(n => n.Id).Distinct().Count() != chart.Nodes.Count ||
            chart.Nodes.Select(n => n.Number).Distinct().Count() != chart.Nodes.Count ||
            chart.NextNumber <= chart.Nodes.Select(n => n.Number).DefaultIfEmpty(0).Max() || chart.NextNumber > 1000001)
            throw new ArgumentException("Nodes need unique IDs, titles up to 100 characters and valid dates/durations. Finish cannot precede start.");
        if (!Enum.IsDefined(chart.Kind) || chart.Leads is null || chart.Leads.Count > NetworkCharts.MaxLeads ||
            (chart.Kind == DiagramKind.Network) != (chart.Layout == ChartLayout.Freeform) || chart.Kind == DiagramKind.Hierarchy && chart.Leads.Count > 0)
            throw new ArgumentException("Invalid diagram module or leads.");
        foreach (var n in chart.Nodes)
        {
            if (n.Format is null) throw new ArgumentException("Node formatting is missing."); n.Format.Validate();
            if (n.X.HasValue != n.Y.HasValue || n.X.HasValue && (!double.IsFinite(n.X.Value) || !double.IsFinite(n.Y!.Value) || n.X < -100000 || n.Y < -100000 || n.X > 100000 || n.Y > 100000) ||
                chart.Kind == DiagramKind.Network && (n.ParentId.HasValue || !n.X.HasValue)) throw new ArgumentException("Invalid free node position.");
        }
        if (chart.Leads.Any(l => l is null || l.Id == Guid.Empty || l.From == l.To || !chart.Nodes.Any(n => n.Id == l.From) || !chart.Nodes.Any(n => n.Id == l.To) || !Enum.IsDefined(l.Routing) || !Enum.IsDefined(l.FromSide) || !Enum.IsDefined(l.ToSide) || l.Description is null || l.Description.Length > 500) || chart.Leads.Select(l => l.Id).Distinct().Count() != chart.Leads.Count)
            throw new ArgumentException("Leads need valid endpoints and descriptions up to 500 characters.");
        var index = chart.Nodes.ToDictionary(n => n.Id);
        foreach (var node in chart.Nodes)
        {
            var visited = new HashSet<Guid> { node.Id }; var parent = node.ParentId;
            while (parent.HasValue)
            {
                if (!index.TryGetValue(parent.Value, out var ancestor) || !visited.Add(parent.Value))
                    throw new ArgumentException("A node has a missing parent or a circular hierarchy.");
                if (visited.Count > MaxDepth) throw new ArgumentException("Keep the diagram to 32 levels or fewer.");
                parent = ancestor.ParentId;
            }
        }
    }
    public static List<ChartNode> Children(Diagram chart, Guid? parent) => chart.Nodes.Where(n => n.ParentId == parent).OrderBy(n => n.Order).ThenBy(n => n.Number).ToList();
    public static List<OutlineNode> Outline(Diagram chart, bool visibleOnly = false)
    {
        Validate(chart); var result = new List<OutlineNode>();
        void Visit(Guid? parent, string prefix, int level)
        {
            var children = Children(chart, parent);
            for (var i = 0; i < children.Count; i++)
            {
                var node = children[i]; var code = prefix + (i + 1);
                result.Add(new(node, code, level, chart.Nodes.Any(n => n.ParentId == node.Id)));
                if (!visibleOnly || !node.Collapsed) Visit(node.Id, code + ".", level + 1);
            }
        }
        Visit(null, "", 1); return result;
    }
    public static ChartNode Add(Diagram chart, Guid? selectedId, bool child, string title = "New node")
    {
        if (chart.Nodes.Count >= MaxNodes) throw new ArgumentException("This diagram has reached its 1,000-node limit.");
        MindMapPlacement.AssignSides(chart);
        var selected = selectedId.HasValue ? chart.Nodes.Single(n => n.Id == selectedId) : null;
        var parentId = child ? selected?.Id : selected?.ParentId;
        var siblings = Children(chart, parentId);
        var position = child || selected is null ? siblings.Count : siblings.FindIndex(n => n.Id == selected.Id) + 1;
        for (var i = 0; i < siblings.Count; i++) siblings[i].Order = i < position ? i : i + 1;
        if (child && selected is not null) selected.Collapsed = false;
        var node = new ChartNode { ParentId = parentId, Order = position, Number = chart.NextNumber++, Title = title.Trim() };
        chart.Nodes.Add(node); Validate(chart); MindMapPlacement.AssignSides(chart); return node;
    }
    public static HashSet<Guid> Descendants(Diagram chart, Guid id)
    {
        var result = new HashSet<Guid> { id }; var pending = new Queue<Guid>(); pending.Enqueue(id);
        while (pending.TryDequeue(out var parent)) foreach (var node in chart.Nodes.Where(n => n.ParentId == parent)) if (result.Add(node.Id)) pending.Enqueue(node.Id);
        return result;
    }
    public static void Delete(Diagram chart, Guid id)
    {
        MindMapPlacement.AssignSides(chart);
        var ids = Descendants(chart, id); chart.Nodes.RemoveAll(n => ids.Contains(n.Id)); chart.Leads.RemoveAll(l => ids.Contains(l.From) || ids.Contains(l.To)); Normalise(chart); Validate(chart);
    }
    public static void Move(Diagram chart, Guid id, Guid? parent)
    {
        var node = chart.Nodes.Single(n => n.Id == id);
        if (parent.HasValue && (!chart.Nodes.Any(n => n.Id == parent) || Descendants(chart, id).Contains(parent.Value)))
            throw new ArgumentException("Choose a parent outside this node's own branch.");
        MindMapPlacement.AssignSides(chart);
        node.ParentId = parent; node.MindMapSide = MindMapBranchSide.Auto; node.Order = Children(chart, parent).Count;
        for (var ancestor = parent; ancestor.HasValue; ancestor = chart.Nodes.Single(n => n.Id == ancestor).ParentId) chart.Nodes.Single(n => n.Id == ancestor).Collapsed = false; Normalise(chart); Validate(chart); MindMapPlacement.AssignSides(chart);
    }
    public static void MoveRelative(Diagram chart, Guid id, Guid target, NodeDrop drop)
    {
        if (chart.Kind != DiagramKind.Hierarchy || !Enum.IsDefined(drop) || id == target || Descendants(chart, id).Contains(target))
            throw new ArgumentException("Drop outside this node's own branch.");
        var moving = chart.Nodes.Single(n => n.Id == id); var destination = chart.Nodes.Single(n => n.Id == target);
        var parent = drop == NodeDrop.Child ? target : destination.ParentId;
        var check = chart.Clone(); var probe = check.Nodes.Single(n => n.Id == id); probe.ParentId = parent; Validate(check);
        MindMapPlacement.AssignSides(chart);
        if (moving.ParentId != parent) moving.MindMapSide = MindMapBranchSide.Auto;
        moving.ParentId = parent;
        var siblings = Children(chart, parent).Where(n => n.Id != id).ToList();
        var at = drop == NodeDrop.Child ? siblings.Count : siblings.FindIndex(n => n.Id == target) + (drop == NodeDrop.After ? 1 : 0);
        siblings.Insert(at, moving); for (var i = 0; i < siblings.Count; i++) siblings[i].Order = i;
        for (var ancestor = parent; ancestor.HasValue; ancestor = chart.Nodes.Single(n => n.Id == ancestor).ParentId) chart.Nodes.Single(n => n.Id == ancestor).Collapsed = false;
        Normalise(chart); Validate(chart); MindMapPlacement.AssignSides(chart);
    }
    public static void Reorder(Diagram chart, Guid id, int direction)
    {
        MindMapPlacement.AssignSides(chart);
        var node = chart.Nodes.Single(n => n.Id == id); var siblings = Children(chart, node.ParentId); var index = siblings.FindIndex(n => n.Id == id);
        var target = index + Math.Sign(direction); if (target < 0 || target >= siblings.Count) return;
        (siblings[index], siblings[target]) = (siblings[target], siblings[index]);
        for (var i = 0; i < siblings.Count; i++) siblings[i].Order = i;
    }
    public static void Indent(Diagram chart, Guid id)
    {
        var node = chart.Nodes.Single(n => n.Id == id); var siblings = Children(chart, node.ParentId); var index = siblings.FindIndex(n => n.Id == id);
        if (index == 0) throw new ArgumentException("Move the node below a sibling first, then indent it beneath that sibling.");
        Move(chart, id, siblings[index - 1].Id);
    }
    public static void Outdent(Diagram chart, Guid id)
    {
        var node = chart.Nodes.Single(n => n.Id == id);
        if (node.ParentId is not Guid parentId) return;
        var parent = chart.Nodes.Single(n => n.Id == parentId); Move(chart, id, parent.ParentId);
    }
    private static void Normalise(Diagram chart)
    {
        foreach (var group in chart.Nodes.GroupBy(n => n.ParentId))
        {
            var nodes = group.OrderBy(n => n.Order).ThenBy(n => n.Number).ToList();
            for (var i = 0; i < nodes.Count; i++) nodes[i].Order = i;
        }
    }
}

public sealed record NodeBox(Guid Id, double X, double Y, double Width, double Height);
public sealed record ChartScene(List<NodeBox> Boxes, double Width, double Height);

public static class ChartGeometry
{
    public const double NodeWidth = 220, NodeHeight = 72, Gap = 28, LevelGap = 66, Margin = 40;
    public static (double Width, double Height) Measure(ChartNode node, int level = 1)
        => NodeMetrics.Measure(node, level);
    // Subtree spans reserve space for every leaf; siblings cannot overlap even at uneven depths.
    public static ChartScene Arrange(Diagram chart, bool includeCollapsed = false, Func<ChartNode, int, (double Width, double Height)>? measure = null)
    {
        measure ??= Measure;
        if (chart.Kind == DiagramKind.Network)
        {
            Charts.Validate(chart);
            var levels = DiagramAppearance.Levels(chart);
            var free = chart.Nodes.Select(n => { var s = measure(n, levels[n.Id]); return new NodeBox(n.Id, n.X!.Value, n.Y!.Value, s.Width, s.Height); }).ToList();
            var index = free.ToDictionary(b => b.Id);
            var routes = chart.Leads.Select(l => LeadGeometry.Route(index[l.From], index[l.To], l.Routing, obstacles: free, fromSide: l.FromSide, toSide: l.ToSide)).ToList();
            return new(free, Math.Max(500, free.Select(b => b.X + b.Width + Margin).Concat(routes.Select(p => Math.Max(p.C1X, p.C2X) + Margin)).DefaultIfEmpty(0).Max()), Math.Max(300, free.Select(b => b.Y + b.Height + Margin).Concat(routes.Select(p => Math.Max(p.C1Y, p.C2Y) + Margin)).DefaultIfEmpty(0).Max()));
        }
        var outline = Charts.Outline(chart, !includeCollapsed); var visible = outline.Select(o => o.Node.Id).ToHashSet();
        var boxes = new List<NodeBox>();
        var hierarchyLevels = outline.ToDictionary(o => o.Node.Id, o => o.Level);
        var measured = outline.ToDictionary(o => o.Node.Id, o => measure(o.Node, o.Level));
        (double Width, double Height) Size(ChartNode node) => measured[node.Id];
        var childIndex = chart.Nodes.Where(n => visible.Contains(n.Id)).GroupBy(n => n.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Order).ThenBy(n => n.Number).ToList());
        List<ChartNode> Kids(Guid? id) => childIndex.GetValueOrDefault(id ?? Guid.Empty) ?? [];
        var widths = outline.GroupBy(o => o.Level - 1).ToDictionary(g => g.Key, g => g.Max(o => Size(o.Node).Width));
        var heights = outline.GroupBy(o => o.Level - 1).ToDictionary(g => g.Key, g => g.Max(o => Size(o.Node).Height));
        double Band(Dictionary<int, double> bands, int depth) => Enumerable.Range(0, depth).Sum(i => bands.GetValueOrDefault(i, NodeWidth) + LevelGap);
        var spans = new Dictionary<(Guid Id, bool Vertical), double>();
        double Span(ChartNode node, bool vertical)
        {
            var key = (node.Id, vertical);
            if (spans.TryGetValue(key, out var cached)) return cached;
            var size = Math.Max(vertical ? Size(node).Width : Size(node).Height, Kids(node.Id).Sum(n => Span(n, vertical) + Gap) - Gap);
            spans[key] = size; return size;
        }
        void Tree(ChartNode node, int depth, double offset, bool vertical, int side)
        {
            var span = Span(node, vertical);
            var size = Size(node);
            var x = vertical ? offset + (span - size.Width) / 2 : side > 0 ? Band(widths, depth) : widths[0] - Band(widths, depth + 1) + LevelGap + widths[depth] - size.Width;
            var y = vertical ? Band(heights, depth) : offset + (span - size.Height) / 2;
            boxes.Add(new(node.Id, x, y, size.Width, size.Height));
            foreach (var c in Kids(node.Id)) { Tree(c, depth + 1, offset, vertical, side); offset += Span(c, vertical) + Gap; }
        }
        if (chart.Layout == ChartLayout.Outline)
        {
            double y = 0;
            foreach (var item in outline) { var size = Size(item.Node); boxes.Add(new(item.Node.Id, (item.Level - 1) * 36, y, size.Width + 160, size.Height)); y += size.Height + 16; }
        }
        else if (chart.Layout == ChartLayout.MindMap)
        {
            var sides = MindMapPlacement.ResolveSides(chart);
            double forestOffset = 0;
            foreach (var root in Kids(null))
            {
                var children = Kids(root.Id);
                var left = children.Where(n => sides[n.Id] == MindMapBranchSide.Left).ToList();
                var right = children.Where(n => sides[n.Id] == MindMapBranchSide.Right).ToList();
                double SideSpan(List<ChartNode> list) => Math.Max(Size(root).Height, list.Sum(n => Span(n, false) + Gap) - Gap);
                var height = Math.Max(SideSpan(left), SideSpan(right));
                boxes.Add(new(root.Id, 0, forestOffset + (height - Size(root).Height) / 2, Size(root).Width, Size(root).Height));
                void Side(List<ChartNode> list, int sign)
                {
                    var offset = forestOffset + (height - SideSpan(list)) / 2;
                    foreach (var child in list) { Tree(child, 1, offset, false, sign); offset += Span(child, false) + Gap; }
                }
                Side(left, -1); Side(right, 1); forestOffset += height + 90;
            }
        }
        else
        {
            var vertical = chart.Layout == ChartLayout.TopDown; double offset = 0;
            foreach (var root in Kids(null)) { Tree(root, 0, offset, vertical, 1); offset += Span(root, vertical) + 90; }
        }
        if (boxes.Count == 0) return new(boxes, 500, 300);
        var minX = boxes.Min(b => b.X); var minY = boxes.Min(b => b.Y);
        boxes = boxes.Select(b => b with { X = b.X - minX + Margin, Y = b.Y - minY + Margin }).ToList();
        return new(boxes, boxes.Max(b => b.X + b.Width) + Margin, boxes.Max(b => b.Y + b.Height) + Margin);
    }
    public static (double X1, double Y1, double X2, double Y2) Connector(NodeBox parent, NodeBox child, ChartLayout layout)
    {
        if (layout == ChartLayout.TopDown) return (parent.X + parent.Width / 2, parent.Y + parent.Height, child.X + child.Width / 2, child.Y);
        if (layout == ChartLayout.Outline) return (parent.X + 12, parent.Y + parent.Height, child.X, child.Y + child.Height / 2);
        var left = child.X < parent.X;
        return (left ? parent.X : parent.X + parent.Width, parent.Y + parent.Height / 2, left ? child.X + child.Width : child.X, child.Y + child.Height / 2);
    }
}
