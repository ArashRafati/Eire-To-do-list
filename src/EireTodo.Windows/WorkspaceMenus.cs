using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace EireTodo.Windows;

internal static class WorkspaceMenus
{
    internal static MenuItem Action(string title,System.Action action,string shortcut="",bool danger=false)
    {
        var item=new MenuItem { Header=title,InputGestureText=shortcut,MinWidth=236 };item.Click+=(_,_)=>action();
        var button=new Button { Foreground=danger?Ui.Error:Ui.Brush("DarkTeal") };RibbonBar.Label(button,title);
        if(button.Content is StackPanel row && row.Children.Count>0) { var icon=row.Children[0];row.Children.RemoveAt(0);item.Icon=icon; }
        if(danger)item.Foreground=Ui.Error;return item;
    }
    internal static void Section(ItemsControl menu,string title)
    {
        if(menu.Items.Count>0)menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header=title.ToUpperInvariant(),IsEnabled=false,FontSize=11.5,Opacity=1,Foreground=Ui.Ink,FontWeight=FontWeights.SemiBold,Padding=new Thickness(8,3,8,3) });
    }
}
