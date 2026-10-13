using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EireTodo.Windows;

internal sealed class CloseWorkspaceDialog : Window
{
    public CloseWorkspaceDialog(System.Func<bool> save, System.Func<bool> saveAs, System.Func<bool> close)
    {
        Ui.ApplyWindowStyle(this); Title="Close SUMAPP"; Width=560; SizeToContent=SizeToContent.Height; ResizeMode=ResizeMode.NoResize; WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new StackPanel { Margin=new Thickness(24) };
        var header=new DockPanel { Margin=new Thickness(0,0,0,18) };
        var logo=new Image { Source=Branding.LoadBundledImage("SumappLogo.png"), Width=40, Height=40, Stretch=Stretch.Uniform, Margin=new Thickness(0,0,12,0) }; header.Children.Add(logo);
        header.Children.Add(new TextBlock { Text="Close SUMAPP",FontSize=23,FontWeight=FontWeights.SemiBold,Foreground=Ui.Ink,VerticalAlignment=VerticalAlignment.Center }); root.Children.Add(header);
        root.Children.Add(new TextBlock { Text="Changes save automatically in your local profile. Save finishes the current edit and saves settings. Save As also writes a complete .sumapp copy to a folder you choose.",TextWrapping=TextWrapping.Wrap,FontSize=16,Margin=new Thickness(0,0,0,12) });
        root.Children.Add(new TextBlock { Text="Close keeps changes already saved, but discards an unfinished title and any unsaved window settings. Cancel returns to your workspace.",TextWrapping=TextWrapping.Wrap,FontSize=14,Margin=new Thickness(0,0,0,20) });
        var outcome=new TextBlock { Foreground=Ui.Ink, TextWrapping=TextWrapping.Wrap, FontSize=14, Margin=new Thickness(0,0,0,12), Visibility=Visibility.Collapsed }; root.Children.Add(outcome);
        var actions=new WrapPanel { HorizontalAlignment=HorizontalAlignment.Right };
        void Action(string label,System.Func<bool> operation,bool primary=false) { var button=Ui.Button(label,(_,_) => { if(operation()) DialogResult=true; else { outcome.Text="Saving was not completed. The app remains open. Use Cancel to return to your edit, or choose another action."; outcome.Visibility=Visibility.Visible; } },primary); button.Margin=new Thickness(6,0,0,6); actions.Children.Add(button); }
        Action("Save",save,true); Action("Save As…",saveAs); var cancel=Ui.Button("Cancel",(_,_) => DialogResult=false); cancel.IsCancel=true; cancel.IsDefault=true; cancel.Margin=new Thickness(6,0,0,6); actions.Children.Add(cancel); Action("Close",close); root.Children.Add(actions); Content=root;
    }
}
