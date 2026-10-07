using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using EireTodo.Core;

namespace EireTodo.Windows;

internal static class WindowsNodeMetrics
{
    private static readonly Dictionary<(string, string, double, bool, bool, int, bool), (double Width, double Height)> cache = [];
    // Measure a real TextBlock with the renderer's exact wrapping, font and line-height settings.
    // The extra two pixels reserve the thicker selection border, so selecting a node cannot clip it.
    public static (double Width, double Height) Measure(ChartNode node, int level)
    {
        var f = node.Format;
        var key = (node.Title, f.Family, f.Size, f.Bold, f.Italic, level, NodeMetrics.HasMetadata(node));
        if (cache.TryGetValue(key, out var result)) return result;
        var text = new TextBlock { Text = node.Title, LineHeight = f.Size * 1.35, LineStackingStrategy = LineStackingStrategy.BlockLineHeight };
        FormattingTools.Apply(text, f);
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = NodeMetrics.Width(text.DesiredSize.Width, f.Size, level);
        text.TextWrapping = TextWrapping.Wrap;
        text.Measure(new Size(width - 28, double.PositiveInfinity));
        result = (width, NodeMetrics.Height(text.DesiredSize.Height, level, NodeMetrics.HasMetadata(node)));
        if (cache.Count > 4096) cache.Clear();
        cache[key] = result; return result;
    }
}
