using System.Text.Json;
using System.Text.Json.Serialization;

namespace EireTodo.Core;

public enum AppMode { Todo, MindMap, Wbs }
public enum ChartLayout { MindMap, RightTree, TopDown, LeftToRight, Outline }

public sealed class ChartNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public Guid? ParentId { get; set; }
    public int Order { get; set; }
    public string Title { get; set; } = "New node";
    public string Notes { get; set; } = "";
    public DateOnly? StartDate { get; set; }
    public DateOnly? FinishDate { get; set; }
    public int DurationDays { get; set; } = 1;
    public bool Completed { get; set; }
    public bool Collapsed { get; set; }
    [JsonIgnore] public string ActivityId => $"A{Number:D5}";
}

public sealed class Diagram
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New diagram";
    public Guid? ProjectId { get; set; }
    public ChartLayout Layout { get; set; } = ChartLayout.MindMap;
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
            !Enum.IsDefined(chart.Layout) || chart.Nodes is null || chart.Nodes.Count > MaxNodes ||
            chart.ScheduleStart.Year < 1900 || chart.ScheduleStart.Year > 2090 || !double.IsFinite(chart.Zoom) || chart.Zoom < .2 || chart.Zoom > 2)
            throw new ArgumentException("Invalid diagram: use a name up to 100 characters and no more than 1,000 nodes.");
        if (chart.Nodes.Any(n => n is null || n.Id == Guid.Empty || n.Number < 1 || n.Number > 1000000 || n.Order < 0 ||
            string.IsNullOrWhiteSpace(n.Title) || n.Title.Length > 100 || n.Notes is null || n.Notes.Length > 250000 ||
            n.DurationDays < 1 || n.DurationDays > 3650 || n.StartDate?.Year < 1900 || n.FinishDate?.Year < 1900 ||
            n.StartDate?.Year > 2090 || n.FinishDate?.Year > 2100 || n.StartDate.HasValue && n.FinishDate < n.StartDate) ||
            chart.Nodes.Select(n => n.Id).Distinct().Count() != chart.Nodes.Count ||
            chart.Nodes.Select(n => n.Number).Distinct().Count() != chart.Nodes.Count ||
            chart.NextNumber <= chart.Nodes.Select(n => n.Number).DefaultIfEmpty(0).Max() || chart.NextNumber > 1000001)
            throw new ArgumentException("Nodes need unique IDs, titles up to 100 characters and valid dates/durations. Finish cannot precede start.");
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
        var selected = selectedId.HasValue ? chart.Nodes.Single(n => n.Id == selectedId) : null;
        var parentId = child ? selected?.Id : selected?.ParentId;
        var siblings = Children(chart, parentId);
        var position = child || selected is null ? siblings.Count : siblings.FindIndex(n => n.Id == selected.Id) + 1;
        for (var i = 0; i < siblings.Count; i++) siblings[i].Order = i < position ? i : i + 1;
        if (child && selected is not null) selected.Collapsed = false;
        var node = new ChartNode { ParentId = parentId, Order = position, Number = chart.NextNumber++, Title = title.Trim() };
        chart.Nodes.Add(node); Validate(chart); return node;
    }
    public static HashSet<Guid> Descendants(Diagram chart, Guid id)
    {
        var result = new HashSet<Guid> { id }; var pending = new Queue<Guid>(); pending.Enqueue(id);
        while (pending.TryDequeue(out var parent)) foreach (var node in chart.Nodes.Where(n => n.ParentId == parent)) if (result.Add(node.Id)) pending.Enqueue(node.Id);
        return result;
    }
    public static void Delete(Diagram chart, Guid id)
    {
        var ids = Descendants(chart, id); chart.Nodes.RemoveAll(n => ids.Contains(n.Id)); Normalise(chart); Validate(chart);
    }
    public static void Move(Diagram chart, Guid id, Guid? parent)
    {
        var node = chart.Nodes.Single(n => n.Id == id);
        if (parent.HasValue && (!chart.Nodes.Any(n => n.Id == parent) || Descendants(chart, id).Contains(parent.Value)))
            throw new ArgumentException("Choose a parent outside this node's own branch.");
        node.ParentId = parent; node.Order = Children(chart, parent).Count;
        for (var ancestor = parent; ancestor.HasValue; ancestor = chart.Nodes.Single(n => n.Id == ancestor).ParentId) chart.Nodes.Single(n => n.Id == ancestor).Collapsed = false; Normalise(chart); Validate(chart);
    }
    public static void Reorder(Diagram chart, Guid id, int direction)
    {
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
    public const double NodeWidth = 260, NodeHeight = 118, Gap = 28, LevelGap = 74, Margin = 40;
    // Subtree spans reserve space for every leaf; siblings cannot overlap even at uneven depths.
    public static ChartScene Arrange(Diagram chart, bool includeCollapsed = false)
    {
        var outline = Charts.Outline(chart, !includeCollapsed); var visible = outline.Select(o => o.Node.Id).ToHashSet();
        var boxes = new List<NodeBox>();
        List<ChartNode> Kids(Guid? id) => Charts.Children(chart, id).Where(n => visible.Contains(n.Id)).ToList();
        double Span(ChartNode node, bool vertical) => Math.Max(vertical ? NodeWidth : NodeHeight,
            Kids(node.Id).Sum(n => Span(n, vertical) + Gap) - Gap);
        void Tree(ChartNode node, int depth, double offset, bool vertical, int side)
        {
            var span = Span(node, vertical);
            var x = vertical ? offset + (span - NodeWidth) / 2 : side * depth * (NodeWidth + LevelGap);
            var y = vertical ? depth * (NodeHeight + LevelGap) : offset + (span - NodeHeight) / 2;
            boxes.Add(new(node.Id, x, y, NodeWidth, NodeHeight));
            foreach (var c in Kids(node.Id)) { Tree(c, depth + 1, offset, vertical, side); offset += Span(c, vertical) + Gap; }
        }
        if (chart.Layout == ChartLayout.Outline)
        {
            for (var i = 0; i < outline.Count; i++) boxes.Add(new(outline[i].Node.Id, (outline[i].Level - 1) * 36, i * (NodeHeight + 16), NodeWidth + 160, NodeHeight));
        }
        else if (chart.Layout == ChartLayout.MindMap)
        {
            double forestOffset = 0;
            foreach (var root in Kids(null))
            {
                var children = Kids(root.Id); var left = children.Where((_, i) => i % 2 == 1).ToList(); var right = children.Where((_, i) => i % 2 == 0).ToList();
                double SideSpan(List<ChartNode> list) => Math.Max(NodeHeight, list.Sum(n => Span(n, false) + Gap) - Gap);
                var height = Math.Max(SideSpan(left), SideSpan(right));
                boxes.Add(new(root.Id, 0, forestOffset + (height - NodeHeight) / 2, NodeWidth, NodeHeight));
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
