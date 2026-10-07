namespace EireTodo.Core;

// A shared, disposable sample for ribbon thumbnails and documented design previews.
public static class DiagramSamples
{
    public static Diagram Create(ChartLayout layout, DiagramPalette palette, NodeDesign design)
    {
        var diagram = Charts.Create("Plan", layout);
        diagram.Palette = palette; diagram.Design = design;
        var first = Charts.Add(diagram, diagram.Nodes[0].Id, true, "Design");
        var second = Charts.Add(diagram, diagram.Nodes[0].Id, true, "Build");
        Charts.Add(diagram, first.Id, true, "Review");
        Charts.Add(diagram, second.Id, true, "Deliver");
        return diagram;
    }
}
