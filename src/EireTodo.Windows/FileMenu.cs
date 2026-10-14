using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EireTodo.Core;
namespace EireTodo.Windows;

public partial class MainWindow
{
    private void SaveProfile()
    {
        if(!chartWorkspace.FinishInlineEdit() || !SaveWindowSettings())return;
        if(TryAction(()=>service.Change(_=>{}))) FileStatus.Text="Saved locally · "+DateTime.Now.ToString("HH:mm:ss");
    }
    private void ShowFileMenu(Button anchor)
    {
        var menu=new ContextMenu { PlacementTarget=anchor,Placement=System.Windows.Controls.Primitives.PlacementMode.Bottom };
        WorkspaceMenus.Section(menu,"Workspace");menu.Items.Add(WorkspaceMenus.Action("Save",SaveProfile,"Ctrl+S"));menu.Items.Add(WorkspaceMenus.Action("Save As…",SaveCopy,"Ctrl+Shift+S"));menu.Items.Add(WorkspaceMenus.Action("Open / import workspace…",()=>RestoreClick(this,new()),"Ctrl+O"));
        WorkspaceMenus.Section(menu,"Export");var chart=WorkspaceMenus.Action("Export diagram…",chartWorkspace.ExportFromFile);chart.IsEnabled=AppModules.FromSelector(ModeSelector.SelectedIndex)!=AppMode.Todo && chartWorkspace.HasDiagram;menu.Items.Add(chart);menu.Items.Add(WorkspaceMenus.Action("Back up all data…",()=>ExportClick(this,new())));
        WorkspaceMenus.Section(menu,"Application");menu.Items.Add(WorkspaceMenus.Action("Data folder",()=>DataFolderClick(this,new())));menu.Items.Add(WorkspaceMenus.Action("Application logo…",ChooseLogo));menu.Items.Add(WorkspaceMenus.Action("Window settings…",()=>WindowOptionsClick(this,new())));menu.Items.Add(new Separator());menu.Items.Add(WorkspaceMenus.Action("Close SUMAPP",Close,"Alt+F4"));menu.IsOpen=true;
    }
    private void FileShortcut(object sender,KeyEventArgs e)
    {
        if(e.SystemKey==Key.F && Keyboard.Modifiers==ModifierKeys.Alt) { ribbon.OpenFile();e.Handled=true; }
        else if(e.Key==Key.S && Keyboard.Modifiers==ModifierKeys.Control){SaveProfile();e.Handled=true;}
        else if(e.Key==Key.S && Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift)){SaveCopy();e.Handled=true;}
        else if(e.Key==Key.O && Keyboard.Modifiers==ModifierKeys.Control){RestoreClick(this,new());e.Handled=true;}
    }
}
