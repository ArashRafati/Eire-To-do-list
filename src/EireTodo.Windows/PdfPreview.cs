using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed class PdfPreviewWindow : Window
{
    private readonly Diagram diagram;
    private readonly Func<byte[],Task<bool>> save;
    private readonly ComboBox paper = new() { ItemsSource = Enum.GetNames<PdfPaper>(), SelectedIndex = 0, Width = 90 };
    private readonly ComboBox orientation = new() { ItemsSource = Enum.GetNames<PdfOrientation>(), SelectedIndex = 0, Width = 140 };
    private readonly CheckBox fit = new() { Content = "Fit to paper", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly CheckBox details = new() { Content = "Include full details", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly ComboBox pages = new() { Width = 145, FontSize = 15 };
    private readonly Border preview = new() { Margin = new Thickness(20), Background = Brushes.White };
    private readonly ScrollViewer scroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button saveButton;
    private PdfDrawing? drawing;
    private int generation;
    private bool saving;
    public PdfPreviewWindow(Diagram diagram,Func<byte[],Task<bool>> save)
    {
        this.diagram = diagram.Clone(); this.save = save; Ui.ApplyWindowStyle(this); Title = "PDF paper preview"; Width = 1020; Height = 760; MinWidth = 600; MinHeight = 440; MaxHeight = Math.Max(440,SystemParameters.WorkArea.Height-32); WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(14) }; var commands = new WrapPanel();
        void Add(string title,Control control) { var field = Ui.Field(title,control); field.Margin = new Thickness(0,0,14,8); commands.Children.Add(field); }
        Add("Paper",paper); Add("Orientation",orientation); Add("Scale",fit); Add("Appendix",details); Add("Preview page",pages); DockPanel.SetDock(commands,Dock.Top); root.Children.Add(commands);
        var footer = new DockPanel { Margin = new Thickness(0,12,0,0) }; var actions = new StackPanel { Orientation = Orientation.Horizontal };
        var printButton = Ui.Button("Print…",(_,_)=>Print()); printButton.Margin=new Thickness(0,0,10,0); actions.Children.Add(printButton);
        var cancel = Ui.Button("Cancel",(_,_)=>Close()); cancel.IsCancel = true; actions.Children.Add(cancel);
        saveButton = Ui.Button("Save PDF…",async (_,_) =>
        {
            if (drawing is null || saving) return; saving = true; saveButton!.IsEnabled = false; commands.IsEnabled = false;
            try { var selected = drawing; var bytes = await Task.Run(()=>DiagramPdf.Write(selected)); if (await save(bytes)) { saving = false; DialogResult = true; } }
            catch(Exception ex) { status.Text = "PDF not saved: " + ex.Message; }
            finally { saving = false; saveButton.IsEnabled = drawing is not null; commands.IsEnabled = true; }
        },true); saveButton.Margin = new Thickness(10,0,0,0); actions.Children.Add(saveButton); DockPanel.SetDock(actions,Dock.Right); footer.Children.Add(actions); footer.Children.Add(status); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
        scroll.Content = preview; root.Children.Add(scroll); Content = root;
        paper.SelectionChanged += (_,_)=>Rebuild(); orientation.SelectionChanged += (_,_)=>Rebuild(); fit.Checked += (_,_)=>Rebuild(); fit.Unchecked += (_,_)=>Rebuild(); details.Checked += (_,_)=>Rebuild(); details.Unchecked += (_,_)=>Rebuild(); pages.SelectionChanged += (_,_)=>ShowPage(); scroll.SizeChanged += (_,_)=>ShowPage(); Loaded += (_,_)=>Rebuild(); Closing += (_,e)=> { if(saving)e.Cancel=true; };
    }
    private async void Rebuild()
    {
        if (!IsLoaded) return; var version = ++generation; saveButton.IsEnabled = false; status.Text = "Preparing paper preview…";
        var options = new PdfOptions((PdfPaper)paper.SelectedIndex,(PdfOrientation)orientation.SelectedIndex,fit.IsChecked==true,details.IsChecked==true);
        try
        {
            var result = await Task.Run(()=>DiagramPdf.Compose(diagram,options)); if (version != generation) return;
            drawing = result; pages.ItemsSource = Enumerable.Range(1,result.Pages.Count).Select(i=>$"Page {i} / {result.Pages.Count}").ToList(); pages.SelectedIndex = 0; ShowPage(); saveButton.IsEnabled = true;
            status.Text = options.FitToPaper ? "Diagram fitted to one sheet. All preview pages are included in the PDF." : "Diagram uses tiled sheets. All preview pages are included in the PDF.";
        }
        catch(Exception ex) { if(version==generation) { drawing=null; status.Text="Preview unavailable: "+ex.Message; } }
    }
    private void Print()
    {
        if (drawing is null || !saveButton.IsEnabled) return;
        try
        {
            var dialog = new PrintDialog(); var ticket = dialog.PrintTicket ?? new System.Printing.PrintTicket();
            ticket.PageOrientation = orientation.SelectedIndex == 0 ? System.Printing.PageOrientation.Landscape : System.Printing.PageOrientation.Portrait;
            ticket.PageMediaSize = new System.Printing.PageMediaSize(paper.SelectedIndex == 0 ? System.Printing.PageMediaSizeName.ISOA4 : System.Printing.PageMediaSizeName.ISOA3); dialog.PrintTicket = ticket;
            if (dialog.ShowDialog() != true) return;
            var document = new System.Windows.Documents.FixedDocument();
            foreach (var sheet in drawing.Pages)
            {
                var page = new System.Windows.Documents.FixedPage { Width = sheet.Width*4/3, Height = sheet.Height*4/3 };
                page.Children.Add(new PdfSheetVisual(sheet) { LayoutTransform = new ScaleTransform(4d/3,4d/3) });
                var content = new System.Windows.Documents.PageContent(); ((System.Windows.Markup.IAddChild)content).AddChild(page); document.Pages.Add(content);
            }
            dialog.PrintDocument(document.DocumentPaginator,drawing.Title);
        }
        catch (Exception ex) { status.Text = "Printing not completed: " + ex.Message; }
    }
    private void ShowPage()
    {
        if (drawing is null || pages.SelectedIndex < 0) return; var sheet=drawing.Pages[pages.SelectedIndex];
        preview.Width = Math.Max(420,scroll.ActualWidth-60); preview.Child = new Viewbox { Child = new PdfSheetVisual(sheet), Stretch = Stretch.Uniform };
    }
}

internal sealed class PdfSheetVisual : FrameworkElement
{
    private readonly PdfSheet sheet;
    private static readonly FontFamily exportFont = new(new Uri("pack://application:,,,/"),"./Assets/#DejaVu Sans");
    public PdfSheetVisual(PdfSheet sheet) { this.sheet=sheet; Width=sheet.Width; Height=sheet.Height; }
    private static Brush Colour(string value) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
    protected override void OnRender(DrawingContext context)
    {
        context.DrawRectangle(Brushes.White,null,new Rect(0,0,sheet.Width,sheet.Height));
        foreach(var layer in sheet.Layers)
        {
            if(layer.Clip is {} clip)context.PushClip(new RectangleGeometry(new Rect(clip.X,clip.Y,clip.Width,clip.Height)));
            context.PushTransform(new TranslateTransform(layer.X,layer.Y)); context.PushTransform(new ScaleTransform(layer.Scale,layer.Scale));
            foreach(var mark in layer.Marks)switch(mark)
            {
                case PdfText text:
                    var typeface=new Typeface(exportFont,text.Italic?FontStyles.Italic:FontStyles.Normal,text.Bold?FontWeights.Bold:FontWeights.Normal,FontStretches.Normal);
                    var formatted=new FormattedText(text.Text,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,typeface,text.Size,Colour(text.Colour),VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    if(text.Underline)formatted.SetTextDecorations(TextDecorations.Underline);
                    var x=text.Centre?-formatted.WidthIncludingTrailingWhitespace/2:0;
                    context.PushTransform(new TranslateTransform(text.X,text.Y));context.PushTransform(new RotateTransform(text.Angle));
                    if(text.Backdrop.Length>0)context.DrawRectangle(Colour(text.Backdrop),null,new Rect(x-4,-text.Size,formatted.WidthIncludingTrailingWhitespace+8,text.Size*1.4));
                    context.DrawText(formatted,new Point(x,-formatted.Baseline));context.Pop();context.Pop();break;
                case PdfBox box:
                    context.DrawRoundedRectangle(Colour(box.Fill),box.Stroke.Length>0?new Pen(Colour(box.Stroke),box.Thickness):null,new Rect(box.X,box.Y,box.Width,box.Height),box.Radius,box.Radius);break;
                case PdfLine line:
                    var geometry=new StreamGeometry();using(var g=geometry.Open()) { var p=line.Points;g.BeginFigure(new Point(p[0].X,p[0].Y),line.Fill.Length>0,line.Closed);if(line.Curve)g.BezierTo(new(p[1].X,p[1].Y),new(p[2].X,p[2].Y),new(p[3].X,p[3].Y),true,false);else foreach(var point in p.Skip(1))g.LineTo(new(point.X,point.Y),true,false); }
                    geometry.Freeze();context.DrawGeometry(line.Fill.Length>0?Colour(line.Fill):null,line.Fill.Length>0?null:new Pen(Colour(line.Colour),line.Thickness),geometry);break;
            }
            context.Pop();context.Pop();if(layer.Clip is not null)context.Pop();
        }
    }
}
