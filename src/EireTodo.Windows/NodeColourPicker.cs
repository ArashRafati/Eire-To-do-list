using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EireTodo.Core;
namespace EireTodo.Windows;

internal sealed class NodeColourPicker : Window
{
    internal NodeColourPicker(int count,Action<string?> apply)
    {
        Ui.ApplyWindowStyle(this);Title="Colour selected nodes";Width=540;Height=650;MinHeight=400;MaxHeight=Math.Max(400,SystemParameters.WorkArea.Height-32);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel { Margin=new Thickness(18) };
        panel.Children.Add(new TextBlock { Text=$"Colour {count} selected node(s)",FontSize=22,FontWeight=FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text="Choose a swatch. Box styles use it as the fill; flat styles use readable text/rule colours. Priority and overdue indicators remain distinct.",TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,8,0,12) });
        var error=new TextBlock { Foreground=Ui.Error,TextWrapping=TextWrapping.Wrap }; panel.Children.Add(error);
        void Set(string? value) { try { apply(value);DialogResult=true; } catch(Exception ex) { error.Text=ex.Message; } }
        var reset=Ui.Button("Use diagram default",(_,_)=>Set(null));reset.HorizontalAlignment=HorizontalAlignment.Left;panel.Children.Add(reset);
        var custom=new TextBox { Text="#039690",Width=112,MaxLength=7,MinHeight=30,Margin=new Thickness(0,0,8,0) };var customRow=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,12) };customRow.Children.Add(custom);customRow.Children.Add(Ui.Button("Apply HEX",(_,_)=>Set(custom.Text.Trim())));panel.Children.Add(customRow);
        foreach(var palette in Enum.GetValues<DiagramPalette>())
        {
            var c=DiagramAppearance.Colours(palette);var row=new DockPanel { Margin=new Thickness(0,3,0,3) };row.Children.Add(new TextBlock { Text=c.Name,Width=236,VerticalAlignment=VerticalAlignment.Center,FontSize=14 });
            foreach(var value in new[]{c.Root,c.Branch,c.Leaf}.Distinct())
            {
                var swatch=new Button { Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)),Style=(Style)Application.Current.FindResource("RibbonButton"),Width=46,Height=30,Padding=new Thickness(0),Margin=new Thickness(3,0,0,0),ToolTip=value+" · "+c.Name };
                // Ribbon chrome stays flat; an explicit swatch is the colour-selection control.
                swatch.Content=new Border { Background=swatch.Background,Width=38,Height=22,CornerRadius=new CornerRadius(3),BorderBrush=Ui.Brush("LineBrush"),BorderThickness=new Thickness(1) };System.Windows.Automation.AutomationProperties.SetName(swatch,c.Name+" "+value);swatch.Click+=(_,_)=>Set(value);row.Children.Add(swatch);
            }
            panel.Children.Add(row);
        }
        Content=new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
    }
}
internal sealed partial class ChartWorkspace
{
    private void ColourSelectedNodes()
    {
        if(!FinishInlineEdit() || Current is null || selectedNodes.Count==0)return;
        var ids=selectedNodes.ToArray();new NodeColourPicker(ids.Length,value=> { if(Current is not Diagram chart)return;var candidate=chart.Clone();NodeColours.Set(candidate,ids,value);Commit(candidate);RefreshData(); }) { Owner=OwnerWindow }.ShowDialog();
    }
}
