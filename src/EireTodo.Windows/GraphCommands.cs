using EireTodo.Core;
namespace EireTodo.Windows;
internal sealed partial class ChartWorkspace
{
    private void AddGraphPeer(IncomingPeerRule rule=IncomingPeerRule.SameSource)
    {
        if(!FinishInlineEdit() || !networkMode)return;
        if(Current is not Diagram chart){NewDiagram();return;}
        Guid? added=null;
        if(!Try(()=> {var candidate=chart.Clone();var node=GraphPeers.Add(candidate,selectedId,rule,WindowsNodeMetrics.Measure);added=node.Id;Commit(candidate);}))return;
        selectedId=added;RefreshData();viewport.Focus();ScrollToSelection();BeginTitle();
    }
    private void OrganiseGraph()
    {
        if(!FinishInlineEdit() || !networkMode || Current is not Diagram chart)return;
        if(!Try(()=> {var candidate=chart.Clone();GraphOrganisation.Organise(candidate,WindowsNodeMetrics.Measure,Math.Clamp(viewport.ViewportWidth/displayZoom,1000,16000));Commit(candidate);}))return;
        RefreshData();Fit();status.Text="Graph organised by connections and measured node sizes · Ctrl+Z restores previous positions.";
    }
}
