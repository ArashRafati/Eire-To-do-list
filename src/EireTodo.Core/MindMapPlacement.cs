namespace EireTodo.Core;

/// <summary>Balances branch footprints while retaining the side of existing branches.</summary>
public static class MindMapPlacement
{
    public static Dictionary<Guid, MindMapBranchSide> ResolveSides(Diagram chart)
    {
        // Use the full hierarchy for side assignment. Folding changes spacing, not branch sides.
        var children = chart.Nodes.GroupBy(n => n.ParentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Order).ThenBy(n => n.Number).ToList());
        List<ChartNode> Kids(Guid id) => children.GetValueOrDefault(id) ?? [];
        var footprints = new Dictionary<Guid, double>();
        double Height(ChartNode node)
        {
            if (footprints.TryGetValue(node.Id, out var height)) return height;
            height = Math.Max(ChartGeometry.NodeHeight, Kids(node.Id).Sum(n => Height(n) + ChartGeometry.Gap) - ChartGeometry.Gap);
            footprints[node.Id] = height; return height;
        }
        var result = new Dictionary<Guid, MindMapBranchSide>();
        foreach (var root in Kids(Guid.Empty))
        {
            var branches = Kids(root.Id); double left = 0, right = 0;
            void Place(ChartNode node, MindMapBranchSide side)
            {
                result[node.Id] = side;
                if (side == MindMapBranchSide.Left) left += Height(node) + ChartGeometry.Gap;
                else right += Height(node) + ChartGeometry.Gap;
            }
            foreach (var node in branches.Where(n => n.MindMapSide != MindMapBranchSide.Auto)) Place(node, node.MindMapSide);
            // Largest unassigned branches first gives compact initial placement for old maps and rebalancing.
            foreach (var node in branches.Where(n => n.MindMapSide == MindMapBranchSide.Auto).OrderByDescending(Height).ThenBy(n => n.Order).ThenBy(n => n.Number))
                Place(node, left < right ? MindMapBranchSide.Left : MindMapBranchSide.Right);
        }
        return result;
    }
    public static void AssignSides(Diagram chart)
    {
        foreach (var (id, side) in ResolveSides(chart)) chart.Nodes.First(n => n.Id == id).MindMapSide = side;
    }
    public static void Rebalance(Diagram chart)
    {
        Charts.Validate(chart);
        var roots = chart.Nodes.Where(n => n.ParentId is null).Select(n => n.Id).ToHashSet();
        foreach (var node in chart.Nodes.Where(n => n.ParentId.HasValue && roots.Contains(n.ParentId.Value))) node.MindMapSide = MindMapBranchSide.Auto;
        AssignSides(chart);
    }
}
