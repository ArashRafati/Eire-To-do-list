namespace EireTodo.Core;

public sealed partial class TodoService
{
    public void SaveDiagram(Diagram chart)
    {
        Charts.Validate(chart); var copy = chart.Clone(); MindMapPlacement.AssignSides(copy);
        Change(d => { var i = d.Diagrams.FindIndex(c => c.Id == copy.Id); if (i < 0) d.Diagrams.Add(copy); else d.Diagrams[i] = copy; });
    }
    public void DeleteDiagram(Guid id) => Change(d => { d.Diagrams.RemoveAll(c => c.Id == id); if (d.Settings.SelectedDiagramId == id) d.Settings.SelectedDiagramId = null; });
}
