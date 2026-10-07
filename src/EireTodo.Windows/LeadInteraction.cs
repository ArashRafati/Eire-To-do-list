using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed partial class ChartWorkspace
{
    private Guid? selectedLeadId;
    private void SelectLead(Guid id)
    {
        if (!FinishInlineEdit()) return; Select(null); selectedLeadId = id; viewport.Focus();
        if (Current is Diagram chart) DrawLeads(chart, scene.Boxes.ToDictionary(b => b.Id));
        status.Text = "Lead selected · Delete: remove · F2 / double-click: label · drag either endpoint to a node side";
    }
    private void DeleteSelectedLead()
    {
        if (selectedLeadId is not Guid id) return;
        Modify(c => c.Leads.RemoveAll(l => l.Id == id));
        if (Current?.Leads.Any(l => l.Id == id) != true) selectedLeadId = null;
    }
    private void EditSelectedLead() { if (selectedLeadId is Guid id) EditLead(id); else status.Text = "Click a lead first, then choose Lead label."; }
    private static StreamGeometry PathGeometry(LeadPath path, LeadRouting routing)
    {
        var geometry = new StreamGeometry(); using (var g = geometry.Open())
        {
            g.BeginFigure(new Point(path.X1,path.Y1),false,false);
            if (routing == LeadRouting.Curve) g.BezierTo(new(path.C1X,path.C1Y),new(path.C2X,path.C2Y),new(path.X2,path.Y2),true,false);
            else foreach (var p in LeadGeometry.Vertices(path).Skip(1)) g.LineTo(new(p.X,p.Y),true,false);
        }
        geometry.Freeze(); return geometry;
    }
    private void DrawLeads(Diagram chart, Dictionary<Guid, NodeBox> boxes)
    {
        foreach (var view in leadViews) canvas.Children.Remove(view); leadViews.Clear();
        if (selectedLeadId.HasValue && !chart.Leads.Any(l => l.Id == selectedLeadId)) selectedLeadId = null;
        var colours = DiagramAppearance.Colours(chart.Palette);
        foreach (var group in chart.Leads.GroupBy(l => l.From.CompareTo(l.To) < 0 ? (l.From,l.To) : (l.To,l.From)))
        {
            var leads = group.ToList();
            for (var i = 0; i < leads.Count; i++)
            {
                var lead = leads[i]; if (!boxes.TryGetValue(lead.From,out var from) || !boxes.TryGetValue(lead.To,out var to)) continue;
                var p = LeadGeometry.Route(from,to,lead.Routing,i-leads.Count/2,boxes.Values.ToList(),lead.FromSide,lead.ToSide);
                var geometry = PathGeometry(p,lead.Routing); var selected = selectedLeadId == lead.Id;
                var hit = new Path { Data = geometry, Stroke = Brushes.Transparent, StrokeThickness = 18, ToolTip = "Click: select · Delete: remove · double-click / F2: label · drag endpoint handles to node sides" };
                var line = new Path { Data = geometry, Stroke = Colour(selected ? colours.Accent : colours.Lead), StrokeThickness = selected ? 3 : 2, IsHitTestVisible = false };
                void Click(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 2) { SelectLead(lead.Id); EditLead(lead.Id); } else SelectLead(lead.Id); e.Handled = true; }
                hit.MouseLeftButtonDown += Click;
                var menu = new ContextMenu(); var edit = new MenuItem { Header = "Label / direction / endpoints · F2" }; edit.Click += (_, _) => { SelectLead(lead.Id); EditLead(lead.Id); }; menu.Items.Add(edit);
                var remove = new MenuItem { Header = "Delete lead" }; remove.Click += (_, _) => { SelectLead(lead.Id); DeleteSelectedLead(); }; menu.Items.Add(remove);
                hit.ContextMenu = menu; hit.PreviewMouseRightButtonDown += (_, e) => { e.Handled = true; SelectLead(lead.Id); menu.PlacementTarget = canvas; menu.IsOpen = true; }; Put(hit); Put(line);
                Arrow(p.X2,p.Y2,p.C2X,p.C2Y); if (lead.DoubleHeaded) Arrow(p.X1,p.Y1,p.C1X,p.C1Y);
                if (!string.IsNullOrWhiteSpace(lead.Description))
                {
                    var text = new TextBlock { Text = lead.Description, FontSize = 14, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = p.LabelWidth, ToolTip = lead.Description };
                    var label = new Border { Child = text, Background = (Brush)Application.Current.FindResource("CanvasBrush"), Padding = new Thickness(5,2,5,2), RenderTransformOrigin = new(.5,.5), RenderTransform = new RotateTransform(p.LabelAngle), ContextMenu = menu };
                    label.Measure(new Size(p.LabelWidth+10,double.PositiveInfinity)); Canvas.SetLeft(label,p.LabelX-label.DesiredSize.Width/2); Canvas.SetTop(label,p.LabelY-label.DesiredSize.Height/2); label.MouseLeftButtonDown += Click; Put(label,1);
                }
                if (selected) { Endpoint(lead,true,new(p.X1,p.Y1)); Endpoint(lead,false,new(p.X2,p.Y2)); }
            }
        }
        void Put(UIElement item,int z = 0) { Panel.SetZIndex(item,z); canvas.Children.Add(item); leadViews.Add(item); }
        void Arrow(double x,double y,double px,double py)
        {
            var angle = Math.Atan2(y-py,x-px)+Math.PI;
            Put(new Polygon { Fill = Colour(colours.Accent), IsHitTestVisible = false, Points = [new(x,y),new(x+11*Math.Cos(angle+.42),y+11*Math.Sin(angle+.42)),new(x+11*Math.Cos(angle-.42),y+11*Math.Sin(angle-.42))] });
        }
        void Endpoint(DiagramLead lead,bool first,Point at)
        {
            var thumb = new Thumb { Width = 16, Height = 16, Cursor = Cursors.Cross, ToolTip = "Drag to the centre of a node's top, right, bottom or left side" };
            var factory = new FrameworkElementFactory(typeof(Ellipse)); factory.SetValue(Shape.FillProperty,Colour(colours.Accent)); factory.SetValue(Shape.StrokeProperty,Brushes.White); factory.SetValue(Shape.StrokeThicknessProperty,1.5); thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
            Canvas.SetLeft(thumb,at.X-8); Canvas.SetTop(thumb,at.Y-8); Put(thumb,4);
            var ports = new List<UIElement>(); Path? ghost = null; (Guid Node,LeadSide Side,double Distance)? candidate = null;
            thumb.DragStarted += (_, _) =>
            {
                foreach (var box in boxes.Values.Where(b => b.Id != (first ? lead.To : lead.From))) foreach (var side in Enum.GetValues<LeadSide>().Where(s => s != LeadSide.Auto))
                {
                    var port = LeadGeometry.Port(box,side); var dot = new Ellipse { Width = 9, Height = 9, Fill = Colour(colours.Accent), Stroke = Brushes.White, StrokeThickness = 1, IsHitTestVisible = false }; Canvas.SetLeft(dot,port.X-4.5); Canvas.SetTop(dot,port.Y-4.5); Panel.SetZIndex(dot,4); ports.Add(dot); canvas.Children.Add(dot);
                }
                ghost = new Path { Stroke = Colour(colours.Accent), StrokeThickness = 2, StrokeDashArray = new DoubleCollection([4,3]), IsHitTestVisible = false }; Panel.SetZIndex(ghost,3); canvas.Children.Add(ghost);
            };
            thumb.DragDelta += (_, _) =>
            {
                var cursor = Mouse.GetPosition(canvas);
                candidate = boxes.Values.Where(b => b.Id != (first ? lead.To : lead.From)).SelectMany(b => Enum.GetValues<LeadSide>().Where(s => s != LeadSide.Auto).Select(s => { var p = LeadGeometry.Port(b,s); return (Node:b.Id,Side:s,Distance:Math.Sqrt(Math.Pow(p.X-cursor.X,2)+Math.Pow(p.Y-cursor.Y,2))); })).OrderBy(p => p.Distance).FirstOrDefault();
                if (candidate is { } found && ghost is not null)
                {
                    var start = first ? boxes[found.Node] : boxes[lead.From]; var end = first ? boxes[lead.To] : boxes[found.Node];
                    ghost.Data = PathGeometry(LeadGeometry.Route(start,end,lead.Routing,fromSide:first ? found.Side : lead.FromSide,toSide:first ? lead.ToSide : found.Side),lead.Routing);
                    status.Text = $"Drop endpoint on {chart.Nodes.Single(n => n.Id == found.Node).Title} · {found.Side}";
                }
            };
            thumb.DragCompleted += (_, e) =>
            {
                foreach (var port in ports) canvas.Children.Remove(port); ports.Clear(); if (ghost is not null) canvas.Children.Remove(ghost);
                if (!e.Canceled && candidate is { } found && found.Distance * displayZoom <= 55)
                    Modify(c => { var edited = c.Leads.Single(l => l.Id == lead.Id); if (first) { edited.From = found.Node; edited.FromSide = found.Side; } else { edited.To = found.Node; edited.ToSide = found.Side; } });
                else status.Text = "Endpoint unchanged. Drop close to one of the highlighted side centres.";
            };
        }
    }
    private void EditLead(Guid id)
    {
        if (!FinishInlineEdit() || Current is not Diagram chart || !chart.Leads.Any(l => l.Id == id)) return;
        var lead = chart.Leads.Single(l => l.Id == id); var dialog = new Window { Title = "Lead label and direction", Width = 600, Height = 560, MinWidth = 450, MinHeight = 400, MaxHeight = Math.Max(400,SystemParameters.WorkArea.Height-32), Owner = OwnerWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner }; Ui.ApplyWindowStyle(dialog);
        var description = new TextBox { Text = lead.Description, MaxLength = 500, MinHeight = 70, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
        var heads = new CheckBox { Content = "Double lead · arrows at both ends", IsChecked = lead.DoubleHeaded, Margin = new Thickness(0,12,0,12) };
        var route = new ComboBox { ItemsSource = new[] { "Curve", "Sharp bends" }, SelectedIndex = (int)lead.Routing };
        var fromSide = new ComboBox { ItemsSource = Enum.GetNames<LeadSide>(), SelectedIndex = (int)lead.FromSide }; var toSide = new ComboBox { ItemsSource = Enum.GetNames<LeadSide>(), SelectedIndex = (int)lead.ToSide };
        var reverse = new CheckBox { Content = "Reverse direction", Margin = new Thickness(0,12,0,12) }; var error = new TextBlock { Foreground = OverdueBrush, TextWrapping = TextWrapping.Wrap };
        var panel = new StackPanel(); panel.Children.Add(Ui.Field("Lead label · shown on the line",description)); panel.Children.Add(heads); panel.Children.Add(Ui.Field("Lead shape",route)); panel.Children.Add(Ui.Field("Start node side",fromSide)); panel.Children.Add(Ui.Field("End node side",toSide)); panel.Children.Add(reverse); panel.Children.Add(error);
        void Save()
        {
            try { var copy = chart.Clone(); var edited = copy.Leads.Single(l => l.Id == id); edited.Description = description.Text.Trim(); edited.DoubleHeaded = heads.IsChecked == true; edited.Routing = (LeadRouting)route.SelectedIndex; edited.FromSide = (LeadSide)fromSide.SelectedIndex; edited.ToSide = (LeadSide)toSide.SelectedIndex;
                if (reverse.IsChecked == true) { (edited.From,edited.To) = (edited.To,edited.From); (edited.FromSide,edited.ToSide) = (edited.ToSide,edited.FromSide); } Commit(copy); dialog.DialogResult = true; RefreshData(); }
            catch (Exception ex) { error.Text = "Lead not saved: " + ex.Message; }
        }
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,12,0,0) }; var cancel = Ui.Button("Cancel",(_,_)=>dialog.Close()); cancel.IsCancel = true; buttons.Children.Add(cancel); var save = Ui.Button("Save lead",(_,_)=>Save(),true); save.Margin = new Thickness(8,0,0,0); save.IsDefault = true; buttons.Children.Add(save);
        var root = new DockPanel { Margin = new Thickness(18) }; DockPanel.SetDock(buttons,Dock.Bottom); root.Children.Add(buttons); root.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); dialog.Content = root;
        dialog.Loaded += (_,_) => { description.Focus(); description.SelectAll(); }; dialog.PreviewKeyDown += (_,e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Save(); e.Handled = true; } }; dialog.ShowDialog(); viewport.Focus();
    }
}
