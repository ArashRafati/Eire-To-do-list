using System.Globalization;
using System.Text;

namespace EireTodo.Core;

public enum DiagramPalette { Eire, Ocean, Forest, Violet, Copper, Monochrome }
public enum NodeDesign { Tiered, Cards, Rounded, Minimal }
public sealed record PaletteColours(string Name, string Root, string Branch, string Leaf, string Border, string Lead, string Accent, string Text);
public static class DiagramAppearance
{
    public static PaletteColours Colours(DiagramPalette palette) => palette switch
    {
        DiagramPalette.Ocean => new("Ocean", "#15374B", "#102C3B", "#101D29", "#4C93B5", "#63BEDD", "#63D7F2", "#FFFFFF"),
        DiagramPalette.Forest => new("Forest", "#1C3C2D", "#173125", "#112219", "#568C69", "#72B28B", "#A5D67A", "#FFFFFF"),
        DiagramPalette.Violet => new("Violet", "#352D53", "#28233E", "#1D1A2E", "#8E7ABB", "#AA94D6", "#D0B3FF", "#FFFFFF"),
        DiagramPalette.Copper => new("Copper", "#493024", "#34251D", "#241B17", "#A17B60", "#CB9870", "#FFC18B", "#FFFFFF"),
        DiagramPalette.Monochrome => new("Monochrome", "#303741", "#232B34", "#151D27", "#9DAABD", "#BBC8D8", "#FFFFFF", "#FFFFFF"),
        _ => new("Eire · original", "#1C2A39", "#101721", "#101721", "#42546B", "#6985A1", "#FFCC33", "#FFFFFF")
    };
    public static double Radius(NodeDesign design, int level) => design switch { NodeDesign.Cards => 2, NodeDesign.Rounded => 18, NodeDesign.Minimal => 0, _ => level == 1 ? 12 : level == 2 ? 6 : 2 };
    public static string Fill(Diagram diagram, int level) { var c = Colours(diagram.Palette); return level == 1 ? c.Root : level == 2 ? c.Branch : c.Leaf; }
    public static Dictionary<Guid, int> Levels(Diagram diagram)
    {
        if (diagram.Kind == DiagramKind.Hierarchy) return Charts.Outline(diagram).ToDictionary(n => n.Node.Id, n => n.Level);
        var levels = new Dictionary<Guid, int>(); var queue = new Queue<Guid>();
        var roots = diagram.Nodes.Where(n => !diagram.Leads.Any(l => l.To == n.Id)).ToList();
        if (roots.Count == 0 && diagram.Nodes.Count > 0) roots.Add(diagram.Nodes.OrderBy(n => n.Order).First());
        foreach (var root in roots) { levels[root.Id] = 1; queue.Enqueue(root.Id); }
        var outgoing = diagram.Leads.GroupBy(l => l.From).ToDictionary(g => g.Key, g => g.Select(l => l.To).ToList());
        while (queue.TryDequeue(out var id)) foreach (var target in outgoing.GetValueOrDefault(id) ?? []) if (!levels.ContainsKey(target)) { levels[target] = Math.Min(32, levels[id] + 1); queue.Enqueue(target); }
        foreach (var node in diagram.Nodes) levels.TryAdd(node.Id, 1);
        return levels;
    }
    // Conservative widths leave room for proportional fonts and long unbroken titles.
    public static int TitleLines(string title, double size, double width)
    {
        double used = 0; int lines = 1;
        foreach (var rune in title.EnumerateRunes())
        {
            if (rune.Value == '\n') { lines++; used = 0; continue; }
            var units = rune.Value > 0x2FF ? 1.2 : "MW@%#".Contains(rune.ToString(), StringComparison.Ordinal) ? 1.05 : Rune.IsWhiteSpace(rune) ? .42 : char.IsUpper((char)rune.Value) ? .8 : .72;
            var advance = size * units;
            if (used + advance > width && used > 0) { lines++; used = 0; }
            used += advance;
        }
        // Word wrapping can leave up to half a line unused.
        return lines == 1 ? 1 : (int)Math.Ceiling(lines * 1.35);
    }
}

public sealed record HierarchySlot(Guid Target, NodeDrop Drop, double X, double Y, double Width, double Height);
public static class HierarchyDropSlots
{
    public static List<HierarchySlot> Create(Diagram diagram, ChartScene scene)
    {
        var boxes = scene.Boxes.ToDictionary(b => b.Id); var result = new List<HierarchySlot>();
        foreach (var group in diagram.Nodes.Where(n => boxes.ContainsKey(n.Id)).GroupBy(n => n.ParentId))
        {
            var siblings = group.OrderBy(n => n.Order).ThenBy(n => n.Number).ToList();
            foreach (var node in siblings)
            {
                var b = boxes[node.Id];
                result.Add(diagram.Layout == ChartLayout.TopDown ? new(node.Id, NodeDrop.Before, b.X - ChartGeometry.Gap, b.Y - 8, ChartGeometry.Gap, b.Height + 16) : new(node.Id, NodeDrop.Before, b.X - 8, b.Y - ChartGeometry.Gap, b.Width + 16, ChartGeometry.Gap));
            }
            var last = boxes[siblings[^1].Id];
            result.Add(diagram.Layout == ChartLayout.TopDown ? new(last.Id, NodeDrop.After, last.X + last.Width, last.Y - 8, ChartGeometry.Gap, last.Height + 16) : new(last.Id, NodeDrop.After, last.X - 8, last.Y + last.Height, last.Width + 16, ChartGeometry.Gap));
        }
        return result;
    }
}
