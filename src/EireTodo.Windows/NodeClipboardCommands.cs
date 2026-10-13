using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using EireTodo.Core;

namespace EireTodo.Windows;

internal sealed partial class ChartWorkspace
{
    private const string ClipboardFormat = "SUMAPP.Nodes.v1";
    private readonly HashSet<Guid> selectedNodes = [];
    private void CopyNodes()
    {
        if (!FinishInlineEdit() || Current is not Diagram chart || selectedNodes.Count == 0) return;
        Try(() =>
        {
            var copied = NodeClipboard.Copy(chart,selectedNodes);
            var data = new DataObject(); data.SetData(ClipboardFormat,JsonSerializer.Serialize(copied,DataDocument.JsonOptions));
            data.SetText(string.Join(Environment.NewLine,Charts.Outline(copied).Select(o => o.Code + " " + o.Node.Title)));
            Clipboard.SetDataObject(data,true); status.Text=$"Copied {copied.Nodes.Count} nodes. Select a parent and press Ctrl+V.";
        });
    }
    private void PasteNodes()
    {
        if (!FinishInlineEdit() || Current is not Diagram chart) return;
        List<Guid>? pasted = null;
        if (!Try(() =>
        {
            if (!Clipboard.ContainsData(ClipboardFormat)) throw new ArgumentException("Copy nodes in SUMAPP first. Text-only clipboard content cannot be pasted as nodes.");
            var json=Clipboard.GetData(ClipboardFormat) as string;
            if (json is null || json.Length > 32*1024*1024) throw new ArgumentException("The node clipboard is missing or too large.");
            var payload=JsonSerializer.Deserialize<Diagram>(json,DataDocument.JsonOptions) ?? throw new ArgumentException("Invalid node clipboard.");
            var candidate=chart.Clone(); pasted=NodeClipboard.Paste(candidate,payload,selectedId,WindowsNodeMetrics.Measure); Commit(candidate);
        })) return;
        selectedNodes.Clear(); selectedNodes.UnionWith(pasted!); selectedId=pasted![0]; RefreshData(); viewport.Focus(); ScrollToSelection();
        status.Text=$"Pasted {pasted.Count} nodes · Ctrl+Z to undo.";
    }
    private void ToggleNotes() => Modify(c =>
    {
        var show = !c.Nodes.Single(n => n.Id == selectedId).ShowNotes;
        foreach(var n in c.Nodes.Where(n => selectedNodes.Contains(n.Id))) n.ShowNotes=show;
    });
    private static bool IsInsideButton(DependencyObject? source)
    {
        for(var p=source; p is not null; p=p is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(p) : LogicalTreeHelper.GetParent(p)) if(p is Button) return true;
        return false;
    }
}
