using System.Windows.Controls.Primitives;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EireTodo.Core;
namespace EireTodo.Windows;
public partial class MainWindow
{
    internal void CaptureRegisterStyle(RegisterPalette palette,RegisterLayout layout)
    {service.Change(d=>{d.Settings.RegisterPalette=palette;d.Settings.RegisterLayout=layout;});ApplyRegisterAppearance();RefreshRows();}
    internal void CaptureRibbonMode(RibbonDisplayMode mode)=>ribbon.SetDisplayMode(mode);
    private void AddRegisterTools()
    {
        var group=ribbon.Group("Format","Register design");
        RibbonBar.Action(group,"Colour presets…",()=>RegisterPresetMenu(true));
        RibbonBar.Action(group,"Row design…",()=>RegisterPresetMenu(false));
        var view=ribbon.Group("View","Register");RibbonBar.Action(view,"Compact rows",()=>SetRegisterLayout(RegisterLayout.Compact));RibbonBar.Action(view,"Comfortable rows",()=>SetRegisterLayout(RegisterLayout.Comfortable));
    }
    private void RegisterPresetMenu(bool colours)
    {
        var menu=new ContextMenu();
        if(colours)foreach(var palette in Enum.GetValues<RegisterPalette>())
        {
            var c=RegisterAppearance.Colours(palette);var swatch=new StackPanel {Orientation=Orientation.Horizontal};foreach(var fill in new[]{c.Header,c.Row,c.Alternate,c.Accent})swatch.Children.Add(new Border {Width=6,Height=16,Background=Ui.Brush(fill)});
            var item=new MenuItem {Header=c.Name,Icon=swatch,IsCheckable=true,IsChecked=service.Data.Settings.RegisterPalette==palette};item.Click+=(_,_)=>{if(TryAction(()=>service.Change(d=>d.Settings.RegisterPalette=palette)))ApplyRegisterAppearance();};menu.Items.Add(item);
        }
        else foreach(var layout in Enum.GetValues<RegisterLayout>()){var item=new MenuItem {Header=layout.ToString(),IsCheckable=true,IsChecked=service.Data.Settings.RegisterLayout==layout};item.Click+=(_,_)=>SetRegisterLayout(layout);menu.Items.Add(item);}
        menu.PlacementTarget=TaskGrid;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.MousePoint;menu.IsOpen=true;
    }
    private void SetRegisterLayout(RegisterLayout layout){if(TryAction(()=>service.Change(d=>d.Settings.RegisterLayout=layout))){ApplyRegisterAppearance();RefreshRows();}}
    private void ApplyRegisterAppearance()
    {
        var c=RegisterAppearance.Colours(service.Data.Settings.RegisterPalette);var layout=service.Data.Settings.RegisterLayout;
        foreach(var (name,value) in new[]{("RegisterText",c.Text),("RegisterSecondary",c.Text),("RegisterHeader",c.Header),("RegisterHeaderText",c.HeaderText),("RegisterSelected",c.Selected),("RegisterLine",c.Line),("RegisterError",c.Error),("RegisterBadge",c.Header),("RegisterBadgeText",c.HeaderText)})TaskGrid.Resources[name]=Ui.Brush(value);
        TaskGrid.Resources["RegisterTaskPadding"]=new Thickness(8,layout==RegisterLayout.Compact?2:6,8,layout==RegisterLayout.Compact?2:6);
        TaskGrid.Background=Ui.Brush(c.Background);TaskGrid.RowBackground=Ui.Brush(c.Row);TaskGrid.AlternatingRowBackground=Ui.Brush(layout==RegisterLayout.Minimal?c.Row:c.Alternate);TaskGrid.Foreground=Ui.Brush(c.Text);TaskGrid.BorderBrush=Ui.Brush(c.Line);
        TaskGrid.GridLinesVisibility=layout==RegisterLayout.Bordered?DataGridGridLinesVisibility.All:layout==RegisterLayout.Minimal?DataGridGridLinesVisibility.None:DataGridGridLinesVisibility.Horizontal;TaskGrid.HorizontalGridLinesBrush=TaskGrid.VerticalGridLinesBrush=Ui.Brush(c.Line);TaskGrid.RowHeight=double.NaN;
        var header=new Style(typeof(DataGridColumnHeader),(Style)Application.Current.FindResource(typeof(DataGridColumnHeader)));header.Setters.Add(new Setter(Control.BackgroundProperty,Ui.Brush(c.Header)));header.Setters.Add(new Setter(Control.ForegroundProperty,Ui.Brush(c.HeaderText)));header.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,8,10,8)));var headerText=new Style(typeof(TextBlock),(Style)Application.Current.FindResource(typeof(TextBlock)));headerText.Setters.Add(new Setter(TextBlock.ForegroundProperty,Ui.Brush(c.HeaderText)));header.Resources[typeof(TextBlock)]=headerText;TaskGrid.ColumnHeaderStyle=header;
    }
}
