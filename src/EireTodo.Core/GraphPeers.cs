namespace EireTodo.Core;

public enum IncomingPeerRule { SameSource, FromSelectedNode }
public static class GraphPeers
{
    public static ChartNode Add(Diagram chart,Guid? selected,IncomingPeerRule incomingRule=IncomingPeerRule.SameSource,Func<ChartNode,int,(double Width,double Height)>? measure=null)
    {
        Charts.Validate(chart);if(chart.Kind!=DiagramKind.Network||!Enum.IsDefined(incomingRule))throw new ArgumentException("Select a graph and a valid peer rule.");
        var candidate=chart.Clone();var source=selected.HasValue?candidate.Nodes.SingleOrDefault(n=>n.Id==selected):null;if(selected.HasValue&&source is null)throw new ArgumentException("Select an existing node.");
        var outgoing=candidate.Leads.Where(l=>l.From==selected).ToList();var incoming=candidate.Leads.Where(l=>l.To==selected).ToList();
        var node=GraphCreation.Add(candidate,selected,unlinked:true,format:source?.Format,measure:measure);
        void Copy(DiagramLead lead,Guid from,Guid to)
        {
            var copied=NetworkCharts.Connect(candidate,from,to,lead.DoubleHeaded,lead.Routing,lead.Description);copied.FromSide=lead.FromSide;copied.ToSide=lead.ToSide;
        }
        foreach(var lead in outgoing)Copy(lead,node.Id,lead.To);
        var fromSelected=outgoing.Count==0&&incoming.Count>0&&incomingRule==IncomingPeerRule.FromSelectedNode;
        if(fromSelected)NetworkCharts.Connect(candidate,source!.Id,node.Id,routing:incoming[0].Routing);
        else foreach(var lead in incoming)Copy(lead,lead.From,node.Id);
        if(source is not null&&!fromSelected)
        {
            var boxes=ChartGeometry.Arrange(candidate,measure:measure).Boxes;var anchor=boxes.Single(b=>b.Id==source.Id);var cell=boxes.Single(b=>b.Id==node.Id);var others=boxes.Where(b=>b.Id!=node.Id).ToList();
            var desired=cell with {X=anchor.X,Y=anchor.Y+anchor.Height+36};
            while(others.FirstOrDefault(b=>FreePlacement.Intersects(desired,b)) is NodeBox obstacle)desired=desired with {Y=obstacle.Y+obstacle.Height+36};
            if(Math.Abs(desired.Y)<=100000){node.X=desired.X;node.Y=desired.Y;}
        }
        Charts.Validate(candidate);chart.Nodes=candidate.Nodes;chart.Leads=candidate.Leads;chart.NextNumber=candidate.NextNumber;return chart.Nodes.Single(n=>n.Id==node.Id);
    }
}
