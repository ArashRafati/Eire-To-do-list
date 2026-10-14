using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed class OrganisationLogoEditor : Window
{
    internal static BitmapSource Decode(byte[] png)
    {
        LogoAsset.Validate(png); using var stream=new MemoryStream(png,false);
        var frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0]; frame.Freeze(); return frame;
    }
    internal OrganisationLogoEditor(Diagram candidate,Action<Diagram> save)
    {
        Ui.ApplyWindowStyle(this); Title="Diagram organisation logo"; Width=570; Height=590; MinHeight=400; MaxHeight=Math.Max(400,SystemParameters.WorkArea.Height-32); WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel(); panel.Children.Add(new TextBlock { Text="Organisation branding",FontSize=22,FontWeight=FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text="A fixed logo in the top-left of this diagram. It fades out while nodes or leads pass behind it. PDF/print exports reserve a clear header area for the logo.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,12),FontSize=15 });
        var image=new Image { Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left };
        var preview=new Border { Child=image,Background=Brushes.White,Padding=new Thickness(8),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,10) }; panel.Children.Add(preview);
        var error=new TextBlock { Foreground=Ui.Error,TextWrapping=TextWrapping.Wrap };
        var screen=new Slider { Minimum=40,Maximum=300,Value=candidate.OrganisationLogoWidth,TickFrequency=10,IsSnapToTickEnabled=true,Width=280,HorizontalAlignment=HorizontalAlignment.Left };
        var export=new Slider { Minimum=30,Maximum=180,Value=candidate.ExportLogoWidth,TickFrequency=5,IsSnapToTickEnabled=true,Width=280,HorizontalAlignment=HorizontalAlignment.Left };
        var screenLabel=new TextBlock(); var exportLabel=new TextBlock();
        void Refresh()
        {
            screenLabel.Text=$"Workspace maximum width · {screen.Value:0} px"; exportLabel.Text=$"PDF maximum width · {export.Value:0} pt";
            preview.Visibility=candidate.OrganisationLogoPng.Length==0?Visibility.Collapsed:Visibility.Visible;
            if(candidate.OrganisationLogoPng.Length>0) { image.Source=Decode(candidate.OrganisationLogoPng); var size=OrganisationBranding.Fit(candidate.OrganisationLogoPng,screen.Value,220); image.Width=size.Width; image.Height=size.Height; }
        }
        var commands=new WrapPanel(); var upload=Ui.Button("Upload PNG…",(_,_) =>
        {
            var file=new OpenFileDialog { Title="Choose organisation logo",Filter="PNG image (*.png)|*.png",CheckFileExists=true };
            if(file.ShowDialog(this)!=true) return;
            try { if(new FileInfo(file.FileName).Length>LogoAsset.MaxBytes) throw new ArgumentException("Choose a PNG up to 4 MB."); var bytes=File.ReadAllBytes(file.FileName); _=Decode(bytes); candidate.OrganisationLogoPng=bytes; error.Text=""; Refresh(); } catch(Exception ex) { error.Text=ex.Message; }
        },true); upload.Margin=new Thickness(0,0,8,0); commands.Children.Add(upload); commands.Children.Add(Ui.Button("Remove logo",(_,_) => { candidate.OrganisationLogoPng=[]; Refresh(); })); panel.Children.Add(commands);
        panel.Children.Add(Ui.Label("Workspace size")); panel.Children.Add(screenLabel); panel.Children.Add(screen); panel.Children.Add(Ui.Label("Export size")); panel.Children.Add(exportLabel); panel.Children.Add(export); panel.Children.Add(error);
        screen.ValueChanged+=(_,_)=>Refresh(); export.ValueChanged+=(_,_)=>Refresh(); Refresh();
        var root=new DockPanel { Margin=new Thickness(20) }; var footer=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0) };
        var cancel=Ui.Button("Cancel",(_,_)=>Close());cancel.IsCancel=true;footer.Children.Add(cancel);var commit=Ui.Button("Save logo settings",(_,_)=> { try { candidate.OrganisationLogoWidth=screen.Value;candidate.ExportLogoWidth=export.Value;save(candidate);DialogResult=true; }catch(Exception ex){error.Text=ex.Message;} },true);commit.Margin=new Thickness(8,0,0,0);footer.Children.Add(commit);DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);root.Children.Add(new ScrollViewer { Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });Content=root;
    }
}

internal sealed partial class ChartWorkspace
{
    private readonly Image organisationImage=new() { Stretch=Stretch.Uniform,IsHitTestVisible=false };
    private readonly Border organisationOverlay=new() { Background=Brushes.White,Padding=new Thickness(6),CornerRadius=new CornerRadius(3),Margin=new Thickness(2,14,14,14),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Visibility=Visibility.Collapsed };
    private byte[] lastOrganisationLogo=[];
    private bool logoCovered;
    private long lastLogoFrame;
    private void ManageOrganisationLogo()
    {
        if(!FinishInlineEdit() || Current is not Diagram chart ) return;
        if(new OrganisationLogoEditor(chart.Clone(),Commit) { Owner=OwnerWindow }.ShowDialog()==true) RefreshData();
    }
    private void InitialiseOrganisationLogo(Grid stage)
    {
        organisationOverlay.Child=organisationImage; stage.Children.Add(organisationOverlay); Panel.SetZIndex(organisationOverlay,3);
        Loaded+=(_,_)=>CompositionTarget.Rendering+=OrganisationFrame;
        Unloaded+=(_,_)=>CompositionTarget.Rendering-=OrganisationFrame;
    }
    private void RefreshOrganisationLogo()
    {
        var chart=Current;
        if(chart is null || chart.OrganisationLogoPng.Length==0) { organisationOverlay.Visibility=Visibility.Collapsed;return; }
        try
        {
            if(!chart.OrganisationLogoPng.AsSpan().SequenceEqual(lastOrganisationLogo)) { organisationImage.Source=OrganisationLogoEditor.Decode(chart.OrganisationLogoPng); lastOrganisationLogo=chart.OrganisationLogoPng; }
            var size=OrganisationBranding.Fit(chart.OrganisationLogoPng,chart.OrganisationLogoWidth,300);organisationImage.Width=size.Width;organisationImage.Height=size.Height;organisationOverlay.Visibility=Visibility.Visible;
        }
        catch(Exception ex) { organisationOverlay.Visibility=Visibility.Collapsed;status.Text="Organisation logo could not be displayed: "+ex.Message; }
    }
    private void OrganisationFrame(object? sender,EventArgs e)
    {
        if(!organisationOverlay.IsVisible) return;
        var now=Environment.TickCount64;if(now-lastLogoFrame<30)return;lastLogoFrame=now;
        var logo=new Rect(2,14,organisationImage.Width+12,organisationImage.Height+12);
        var covered=false;
        foreach(var view in nodeViews.Values)
        {
            var bounds=new Rect(Canvas.GetLeft(view)*displayZoom-viewport.HorizontalOffset,Canvas.GetTop(view)*displayZoom-viewport.VerticalOffset,view.Width*displayZoom,view.Height*displayZoom);
            if(bounds.IntersectsWith(logo)) { covered=true;break; }
        }
        if(!covered)
        {
            var world=new Rect((logo.X+viewport.HorizontalOffset)/displayZoom,(logo.Y+viewport.VerticalOffset)/displayZoom,logo.Width/displayZoom,logo.Height/displayZoom);
            var rectangle=new RectangleGeometry(world);
            foreach(var visual in leadViews.Concat<UIElement>(hierarchyPaths.Values))
            {
                if(visual is System.Windows.Shapes.Path path && path.Data is Geometry data)
                {
                    var expanded=data.Bounds;if(expanded.IsEmpty)continue;expanded.Inflate(5,5);if(!expanded.IntersectsWith(world))continue;
                    if(data.FillContainsWithDetail(rectangle)!=IntersectionDetail.Empty || data.GetWidenedPathGeometry(new Pen(Brushes.Black,Math.Max(5,path.StrokeThickness))).FillContainsWithDetail(rectangle)!=IntersectionDetail.Empty){covered=true;break;}
                }
                else if(visual is FrameworkElement element)
                {
                    var x=Canvas.GetLeft(element);var y=Canvas.GetTop(element);if(double.IsNaN(x)||double.IsNaN(y))continue;
                    var local=new Rect(0,0,Math.Max(element.ActualWidth,element.DesiredSize.Width),Math.Max(element.ActualHeight,element.DesiredSize.Height));
                    var bounds=element.RenderTransform.TransformBounds(local);bounds.Offset(x,y);
                    if(bounds.IntersectsWith(world)){covered=true;break;}
                }
            }
        }
        if(covered==logoCovered)return;logoCovered=covered;
        organisationOverlay.BeginAnimation(OpacityProperty,new DoubleAnimation(covered?0:1,TimeSpan.FromMilliseconds(180)) { EasingFunction=new QuadraticEase { EasingMode=EasingMode.EaseOut } });
    }
}
