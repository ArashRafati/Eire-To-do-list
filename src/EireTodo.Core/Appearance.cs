using System.Globalization;
using System.Text;

namespace EireTodo.Core;

// Append values: existing saved numeric palette/design values retain their meaning.
public enum DiagramPalette { Eire, Ocean, Forest, Violet, Copper, Monochrome, Blueprint, Navy, Cobalt, Emerald, ForestGreen, Amber, Sunset, Coral, Rose, Plum }
public enum NodeDesign { Tiered, Cards, Rounded, Minimal, Flat, Underlined, Outline, AccentBar, Mixed, Pill }
public enum NodeDecoration { Box, Flat, Underline, AccentBar }
public sealed record NodeSurface(string Fill, string Text, string Stroke, double Radius, NodeDecoration Decoration);
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
        DiagramPalette.Blueprint => Pack("Blueprint", "#164E63", "#CFFAFE", "#ECFEFF", "#0891B2"),
        DiagramPalette.Navy => Pack("Navy & ice", "#1E3A5F", "#DBEAFE", "#EFF6FF", "#3B82F6"),
        DiagramPalette.Cobalt => Pack("Cobalt & lime", "#1E40AF", "#E0E7FF", "#ECFCCB", "#4F46E5"),
        DiagramPalette.Emerald => Pack("Emerald & mint", "#065F46", "#D1FAE5", "#ECFDF5", "#10B981"),
        DiagramPalette.ForestGreen => Pack("Forest & sage", "#365314", "#DCFCE7", "#F0FDF4", "#65A30D"),
        DiagramPalette.Amber => Pack("Amber & charcoal", "#713F12", "#FEF3C7", "#FFFBEB", "#D97706"),
        DiagramPalette.Sunset => Pack("Sunset & peach", "#9A3412", "#FFEDD5", "#FFF7ED", "#F97316"),
        DiagramPalette.Coral => Pack("Coral & slate", "#334155", "#FFE4E6", "#FFF1F2", "#E11D48"),
        DiagramPalette.Rose => Pack("Rose & blush", "#831843", "#FCE7F3", "#FDF2F8", "#DB2777"),
        DiagramPalette.Plum => Pack("Plum & lavender", "#581C87", "#F3E8FF", "#FAF5FF", "#9333EA"),
        _ => Palette("SUMAPP · default", BrandTheme.DarkTeal, BrandTheme.SoftTeal, BrandTheme.SoftTeal)
    };
    private static PaletteColours Palette(string name, string root, string branch, string leaf) => new(name, root, branch, leaf, BrandTheme.Primary, BrandTheme.Primary, BrandTheme.Accent, BrandTheme.DarkTeal);
    private static PaletteColours Pack(string name, string root, string branch, string leaf, string accent) => new(name, root, branch, leaf, accent, accent, accent, root);
    public static string DesignName(NodeDesign design) => design switch { NodeDesign.Tiered => "Tiered boxes", NodeDesign.Cards => "Box cards", NodeDesign.Rounded => "Rounded boxes", NodeDesign.Minimal => "Square boxes", NodeDesign.Flat => "Flat", NodeDesign.Underlined => "Flat · underline", NodeDesign.Outline => "Outline boxes", NodeDesign.AccentBar => "Side accent", NodeDesign.Mixed => "Mixed levels", NodeDesign.Pill => "Pill nodes", _ => design.ToString() };
    public static string DesignDescription(NodeDesign design) => design switch { NodeDesign.Flat => "Text nodes without an enclosing frame", NodeDesign.Underlined => "Flat text nodes with a coloured underline", NodeDesign.Outline => "Neutral boxes with coloured outlines", NodeDesign.AccentBar => "Colour-filled nodes with a left accent bar", NodeDesign.Mixed => "Box root, underlined branches and flat leaves", NodeDesign.Tiered => "Subtle rounded boxes with different corners by level", NodeDesign.Pill => "Colour-filled capsules", _ => "Apply this node shape to the entire diagram" };
    public static NodeSurface Surface(Diagram diagram, ChartNode node, int level)
    {
        var normal = Surface(diagram.Palette, diagram.Design, level);
        // Priority and overdue remain visible in every colour pack and node style.
        return Overdue.IsDue(node.FinishDate, node.Completed) ? normal with { Fill = BrandTheme.ErrorSurface, Text = BrandTheme.Error, Stroke = BrandTheme.Error, Decoration = NodeDecoration.Box } : node.Priority ? normal with { Fill = BrandTheme.Yellow, Text = BrandTheme.Black, Decoration = NodeDecoration.Box } : normal;
    }
    public static NodeSurface Surface(DiagramPalette palette, NodeDesign design, int level)
    {
        var c = Colours(palette);
        var decoration = design switch { NodeDesign.Flat => NodeDecoration.Flat, NodeDesign.Underlined => NodeDecoration.Underline, NodeDesign.AccentBar => NodeDecoration.AccentBar, NodeDesign.Mixed when level == 2 => NodeDecoration.Underline, NodeDesign.Mixed when level > 2 => NodeDecoration.Flat, _ => NodeDecoration.Box };
        var flat = decoration is NodeDecoration.Flat or NodeDecoration.Underline || design == NodeDesign.Outline;
        var fill = flat ? BrandTheme.Surface : level == 1 ? c.Root : level == 2 ? c.Branch : c.Leaf;
        var text = flat ? c.Text : Contrast(c.Text, fill) >= 4.5 ? c.Text : Contrast(BrandTheme.White, fill) >= Contrast(BrandTheme.Ink, fill) ? BrandTheme.White : BrandTheme.Ink;
        return new(fill, text, decoration == NodeDecoration.Flat ? "" : c.Border, Radius(design, level), decoration);
    }
    public static double Contrast(string first, string second)
    {
        static double L(string colour) { var c = Convert.FromHexString(colour[1..]).Select(v => v / 255d).Select(v => v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4)).ToArray(); return .2126 * c[0] + .7152 * c[1] + .0722 * c[2]; }
        var a = L(first); var b = L(second); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    public static string Fill(Diagram diagram, ChartNode node, int level) => Surface(diagram, node, level).Fill;
    public static string Text(Diagram diagram, ChartNode node, int level) => Surface(diagram, node, level).Text;
    // Capping capsule corners keeps code and wrapped titles inside the coloured surface.
    public static double Radius(NodeDesign design, int level) => design switch { NodeDesign.Pill => 24, NodeDesign.Cards => 2, NodeDesign.Rounded => 18, NodeDesign.Flat or NodeDesign.Underlined or NodeDesign.Outline or NodeDesign.AccentBar or NodeDesign.Minimal => 0, _ => level == 1 ? 12 : level == 2 ? 6 : 2 };
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
