using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed partial class ChartWorkspace
{
    private readonly List<Border> insertionSlots = [];
    private static Brush Colour(string value) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));
    private void ShowInsertionSlots(Guid moving)
    {
        ClearInsertionSlots(); if (Current is not Diagram chart) return;
        var excluded = Charts.Descendants(chart, moving);
        foreach (var slot in HierarchyDropSlots.Create(chart, scene).Where(s => !excluded.Contains(s.Target)))
        {
            var vertical = chart.Layout == ChartLayout.TopDown;
            var marker = new Border { Width = vertical ? 3 : Math.Max(8, slot.Width - 12), Height = vertical ? Math.Max(8,slot.Height - 12) : 3, Background = Ui.Accent, Opacity = .45, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            var zone = new Border { Width = slot.Width, Height = slot.Height, Background = Brushes.Transparent, Child = marker, AllowDrop = true };
            Canvas.SetLeft(zone, slot.X); Canvas.SetTop(zone, slot.Y); Panel.SetZIndex(zone, 5);
            zone.DragOver += (_, e) => { e.Handled = true; e.Effects = e.Data.GetData(DragFormat) is NodeDrag d && d.Diagram == chart.Id ? DragDropEffects.Move : DragDropEffects.None; marker.Opacity = 1; status.Text = "Drop between siblings: move branch " + (slot.Drop == NodeDrop.Before ? "before " : "after ") + chart.Nodes.Single(n => n.Id == slot.Target).Title; };
            zone.DragLeave += (_, _) => marker.Opacity = .45;
            zone.Drop += (_, e) => { e.Handled = true; if (e.Data.GetData(DragFormat) is NodeDrag drag && drag.Diagram == Current?.Id) { selectedId = drag.Node; Modify(c => Charts.MoveRelative(c, drag.Node, slot.Target, slot.Drop)); ScrollToSelection(); } };
            insertionSlots.Add(zone); canvas.Children.Add(zone);
        }
    }
    private void ClearInsertionSlots() { foreach (var slot in insertionSlots) canvas.Children.Remove(slot); insertionSlots.Clear(); }
    private void MoveSelected()
    {
        if (!FinishInlineEdit() || Current is not Diagram chart || selectedId is not Guid id) return;
        var excluded = Charts.Descendants(chart,id);
        var items = Charts.Outline(chart).Where(o => !excluded.Contains(o.Node.Id)).Select(o => new ProjectChoice(o.Code + " · " + o.Node.Title, o.Node.Id)).ToList();
        if (items.Count == 0) return;
        var dialog = new Window { Title = "Move branch", Width = 520, Height = 265, Owner = OwnerWindow, WindowStartupLocation = WindowStartupLocation.CenterOwner }; Ui.ApplyWindowStyle(dialog);
        var target = new ComboBox { ItemsSource = items, ItemTemplate = Ui.DisplayTemplate("Label"), SelectedIndex = 0 }; var position = new ComboBox { ItemsSource = new[] { "Before · same level", "After · same level", "Inside · as a child" }, SelectedIndex = 0 };
        var root = new StackPanel { Margin = new Thickness(18) }; root.Children.Add(Ui.Field("Destination node",target)); root.Children.Add(Ui.Field("Position",position));
        root.Children.Add(Ui.Button("Move branch", (_, _) => { if (target.SelectedItem is ProjectChoice choice && Try(() => { var copy = chart.Clone(); Charts.MoveRelative(copy,id,choice.Value!.Value,(NodeDrop)position.SelectedIndex); Commit(copy); })) { dialog.DialogResult = true; RefreshData(); ScrollToSelection(); } }, true)); dialog.Content = root; dialog.ShowDialog();
    }
}
