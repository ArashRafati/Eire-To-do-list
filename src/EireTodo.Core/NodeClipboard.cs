namespace EireTodo.Core;

public static class NodeClipboard
{
    public static Diagram Copy(Diagram source, IEnumerable<Guid> selection)
    {
        Charts.Validate(source);
        var chosen = selection.ToHashSet();
        if (chosen.Count == 0 || chosen.Any(id => !source.Nodes.Any(n => n.Id == id))) throw new ArgumentException("Select nodes to copy.");
        var ids = source.Kind == DiagramKind.Hierarchy ? chosen.SelectMany(id => Charts.Descendants(source,id)).ToHashSet() : chosen;
        var copy = source.Clone(); copy.Id = Guid.NewGuid(); copy.ProjectId = null;
        copy.Nodes.RemoveAll(n => !ids.Contains(n.Id));
        foreach (var n in copy.Nodes) {if (n.ParentId is Guid p && !ids.Contains(p)) n.ParentId = null;n.Shape??=source.Shape;}
        copy.Leads.RemoveAll(l => !ids.Contains(l.From) || !ids.Contains(l.To));
        Charts.Validate(copy); return copy;
    }

    // Validate a detached candidate first: invalid clipboard data never partly alters the target.
    public static List<Guid> Paste(Diagram target, Diagram copied, Guid? parent, Func<ChartNode,int,(double Width,double Height)>? measure = null)
    {
        Charts.Validate(target); Charts.Validate(copied);
        if (copied.Nodes.Count == 0 || target.Nodes.Count + copied.Nodes.Count > Charts.MaxNodes) throw new ArgumentException("Paste would exceed the 1,000-node limit, or the clipboard is empty.");
        if (parent.HasValue && !target.Nodes.Any(n => n.Id == parent)) throw new ArgumentException("Select an existing destination.");
        var candidate = target.Clone(); var payload = copied.Clone();
        var ids = payload.Nodes.ToDictionary(n => n.Id, _ => Guid.NewGuid());
        var roots = Charts.Outline(payload).Where(o => !o.Node.ParentId.HasValue).Select(o => o.Node.Id).ToList();
        var pasted = new List<Guid>();
        var sourceBoxes = ChartGeometry.Arrange(payload,true,measure).Boxes.ToDictionary(b => b.Id);
        var currentBoxes = ChartGeometry.Arrange(candidate,true,measure).Boxes;
        var startX = currentBoxes.Select(b => b.X + b.Width).DefaultIfEmpty(0).Max() + 80;
        var minX = sourceBoxes.Values.Min(b => b.X); var minY = sourceBoxes.Values.Min(b => b.Y);
        var nextOrder = Charts.Children(candidate,parent).Select(n => n.Order).DefaultIfEmpty(-1).Max()+1;
        var graphOrder = candidate.Nodes.Select(n => n.Order).DefaultIfEmpty(-1).Max()+1;
        foreach (var outline in Charts.Outline(payload))
        {
            var n = outline.Node; var oldId = n.Id; var oldParent = n.ParentId;
            n.Id = ids[oldId]; n.Number = candidate.NextNumber++; n.MindMapSide = MindMapBranchSide.Auto;
            n.ParentId = candidate.Kind == DiagramKind.Network ? null : oldParent.HasValue ? ids[oldParent.Value] : parent;
            if (candidate.Kind == DiagramKind.Network) { var box=sourceBoxes[oldId]; n.X=startX+box.X-minX; n.Y=40+box.Y-minY; n.Order=graphOrder++; }
            else { n.X=n.Y=null; if (!oldParent.HasValue) n.Order=nextOrder++; }
            candidate.Nodes.Add(n); pasted.Add(n.Id);
        }
        if (candidate.Kind == DiagramKind.Network)
        {
            foreach (var lead in payload.Leads) { lead.Id=Guid.NewGuid(); lead.From=ids[lead.From]; lead.To=ids[lead.To]; candidate.Leads.Add(lead); }
            if (payload.Kind == DiagramKind.Hierarchy) foreach (var n in copied.Nodes.Where(n => n.ParentId.HasValue)) NetworkCharts.Connect(candidate,ids[n.ParentId!.Value],ids[n.Id]);
            if (parent is Guid from) foreach (var root in roots) NetworkCharts.Connect(candidate,from,ids[root]);
        }
        else if (parent is Guid p) candidate.Nodes.Single(n => n.Id==p).Collapsed=false;
        Charts.Validate(candidate);
        if (candidate.Kind == DiagramKind.Network)
        {
            // New leads can change levels, wrapping and note height. Settle only pasted
            // nodes using final renderer sizes; existing manual positions stay intact.
            var boxes = ChartGeometry.Arrange(candidate,true,measure).Boxes;
            var settled = boxes.Where(b => !pasted.Contains(b.Id)).ToList();
            foreach (var id in pasted)
            {
                var box = boxes.Single(b => b.Id == id); var probe = box;
                while (settled.FirstOrDefault(b => FreePlacement.Intersects(probe,b)) is NodeBox obstacle)
                {
                    probe = probe with { Y = obstacle.Y + obstacle.Height + 28 };
                    if (probe.Y > 100000) throw new ArgumentException("No free position is available for these copied nodes.");
                }
                var node = candidate.Nodes.Single(n => n.Id == id); node.X = probe.X; node.Y = probe.Y; settled.Add(probe);
            }
        }
        Charts.Validate(candidate); MindMapPlacement.AssignSides(candidate);
        target.Nodes=candidate.Nodes; target.Leads=candidate.Leads; target.NextNumber=candidate.NextNumber;
        return pasted;
    }
}
