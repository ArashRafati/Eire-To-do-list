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
        DiagramPalette.Ocean => Palette("Teal · light", BrandTheme.DarkTeal, BrandTheme.SoftTeal, BrandTheme.White),
        DiagramPalette.Forest => Palette("Teal · outline", BrandTheme.DarkTeal, BrandTheme.White, BrandTheme.White),
        DiagramPalette.Violet => Palette("Ink · technical", BrandTheme.Ink, BrandTheme.SoftTeal, BrandTheme.White),
        DiagramPalette.Copper => Palette("Eire · accents", BrandTheme.Black, BrandTheme.SoftTeal, BrandTheme.Surface),
        DiagramPalette.Monochrome => Palette("Neutral · clean", BrandTheme.Ink, BrandTheme.Surface, BrandTheme.White),
        _ => Palette("SUMAPP · default", BrandTheme.DarkTeal, BrandTheme.SoftTeal, BrandTheme.SoftTeal)
    };
    private static PaletteColours Palette(string name, string root, string branch, string leaf) => new(name, root, branch, leaf, BrandTheme.Primary, BrandTheme.Primary, BrandTheme.Accent, BrandTheme.DarkTeal);
    public static string Fill(Diagram diagram, ChartNode node, int level) => Overdue.IsDue(node.FinishDate, node.Completed) ? BrandTheme.ErrorSurface : node.Priority ? BrandTheme.Yellow : Fill(diagram, level);
    public static string Text(Diagram diagram, ChartNode node, int level) => Overdue.IsDue(node.FinishDate, node.Completed) ? BrandTheme.Error : node.Priority ? BrandTheme.Black : level == 1 ? BrandTheme.White : BrandTheme.DarkTeal;
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
