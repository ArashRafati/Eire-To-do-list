namespace EireTodo.Core;

public static class GraphConnections
{
    public static int Unlink(Diagram graph,IEnumerable<Guid> nodes)
    {
        Charts.Validate(graph);var ids=nodes.ToHashSet();if(graph.Kind!=DiagramKind.Network||ids.Count==0||ids.Any(id=>!graph.Nodes.Any(n=>n.Id==id)))throw new ArgumentException("Select existing graph nodes.");
        return graph.Leads.RemoveAll(l=>ids.Contains(l.From)||ids.Contains(l.To));
    }
    public static int ConnectMany(Diagram graph,Guid anchor,IEnumerable<Guid> targets,LeadDirection direction,LeadRouting routing,string label="",bool parallel=false)
    {
        Charts.Validate(graph);var ids=targets.Distinct().ToList();
        if(graph.Kind!=DiagramKind.Network||!graph.Nodes.Any(n=>n.Id==anchor)||ids.Count==0||ids.Any(id=>id==anchor||!graph.Nodes.Any(n=>n.Id==id))||!Enum.IsDefined(direction)||!Enum.IsDefined(routing)||label is null||label.Length>500)throw new ArgumentException("Select a central node and one or more different nodes, with a label up to 500 characters.");
        var candidate=graph.Clone();var created=0;
        foreach(var target in ids)
        {
            var from=direction==LeadDirection.Incoming?target:anchor;var to=direction==LeadDirection.Incoming?anchor:target;var both=direction==LeadDirection.Both;
            var exists=candidate.Leads.Any(l=>both?l.DoubleHeaded&&((l.From==from&&l.To==to)||(l.From==to&&l.To==from)):(l.From==from&&l.To==to)||(l.DoubleHeaded&&l.From==to&&l.To==from));
            if(exists&&!parallel)continue;
            candidate.Leads.Add(new DiagramLead {From=from,To=to,DoubleHeaded=both,Routing=routing,Description=label.Trim()});created++;
        }
        Charts.Validate(candidate);graph.Leads=candidate.Leads;return created;
    }
}
