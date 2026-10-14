using System.Windows;
using System.Windows.Controls;
using EireTodo.Core;
namespace EireTodo.Windows;

internal sealed class GraphConnectionsEditor : Window
{
    private sealed record Choice(Guid Id,string Label);
    internal GraphConnectionsEditor(Diagram graph,Guid? selected,IEnumerable<Guid> preselected,LeadDirection initialDirection,LeadRouting initialRouting,Func<Guid,IEnumerable<Guid>,LeadDirection,LeadRouting,string,bool,int> save)
    {
        Ui.ApplyWindowStyle(this);Title="Connect multiple graph nodes";Width=620;Height=690;MinHeight=440;MaxHeight=Math.Max(440,SystemParameters.WorkArea.Height-32);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var choices=graph.Nodes.OrderBy(n=>n.Order).ThenBy(n=>n.Number).Select(n=>new Choice(n.Id,n.Number+" · "+n.Title)).ToList();var anchor=new ComboBox {ItemsSource=choices,ItemTemplate=Ui.DisplayTemplate("Label",14)};
        var nodes=new ListBox {SelectionMode=SelectionMode.Multiple,ItemTemplate=Ui.DisplayTemplate("Label",14),MinHeight=120,Height=210,BorderBrush=Ui.Brush("LineBrush"),BorderThickness=new Thickness(1)};
        var direction=new ComboBox {ItemsSource=new[]{"Central node → selected nodes","Selected nodes → central node","Central node ↔ selected nodes"},SelectedIndex=(int)initialDirection};var routing=new ComboBox {ItemsSource=new[]{"Curve","Sharp bends"},SelectedIndex=(int)initialRouting};var label=new TextBox {MaxLength=500};var parallel=new CheckBox {Content="Allow parallel leads to already-connected nodes",Margin=new Thickness(0,12,0,0)};
        var error=new TextBlock {Foreground=Ui.Error,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};var selectedIds=preselected.ToHashSet();
        anchor.SelectionChanged+=(_,_)=> {if(anchor.SelectedItem is not Choice c)return;selectedIds.UnionWith(nodes.SelectedItems.Cast<Choice>().Select(x=>x.Id));nodes.ItemsSource=choices.Where(n=>n.Id!=c.Id).ToList();foreach(var item in nodes.Items.Cast<Choice>().Where(n=>selectedIds.Contains(n.Id)))nodes.SelectedItems.Add(item);};anchor.SelectedItem=choices.FirstOrDefault(n=>n.Id==selected)??choices.FirstOrDefault();
        var panel=new StackPanel();panel.Children.Add(new TextBlock {Text="Connect a group of nodes",FontSize=22,FontWeight=FontWeights.SemiBold});panel.Children.Add(Ui.Field("Central node",anchor));panel.Children.Add(Ui.Field("Other nodes · click to select multiple",nodes));panel.Children.Add(Ui.Field("Direction",direction));panel.Children.Add(Ui.Field("Routing",routing));panel.Children.Add(Ui.Field("Lead description (optional, applied to each new lead)",label));panel.Children.Add(parallel);panel.Children.Add(error);
        var root=new DockPanel {Margin=new Thickness(20)};var footer=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};var cancel=Ui.Button("Cancel",(_,_)=>Close());cancel.IsCancel=true;footer.Children.Add(cancel);var create=Ui.Button("Create connections",(_,_)=> {try{if(anchor.SelectedItem is not Choice c)throw new ArgumentException("Choose a central node.");var count=save(c.Id,nodes.SelectedItems.Cast<Choice>().Select(n=>n.Id),(LeadDirection)direction.SelectedIndex,(LeadRouting)routing.SelectedIndex,label.Text,parallel.IsChecked==true);if(count==0){error.Text="These connections already exist. Select other nodes or allow parallel leads.";return;}DialogResult=true;}catch(Exception ex){error.Text=ex.Message;}},true);create.Margin=new Thickness(8,0,0,0);footer.Children.Add(create);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);root.Children.Add(new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Content=root;
    }
}
internal sealed partial class ChartWorkspace
{
    private void ConnectMultiple()
    {
        if(!FinishInlineEdit()||!networkMode||Current is not Diagram graph)return;
        var editor=new GraphConnectionsEditor(graph,selectedId,selectedNodes,Direction,Routing,(anchor,targets,direction,routing,label,parallel)=>
        {
            if(Current is not Diagram chart)throw new InvalidOperationException("Select a graph first.");var candidate=chart.Clone();var count=GraphConnections.ConnectMany(candidate,anchor,targets,direction,routing,label,parallel);if(count>0){Commit(candidate);RefreshData();status.Text=$"Created {count} connections · Ctrl+Z to undo.";}return count;
        }){Owner=OwnerWindow};editor.ShowDialog();
    }
}
