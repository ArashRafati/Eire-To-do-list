using System.Collections.Concurrent;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace EireTodo.Core;

public static class NodeMetrics
{
    private static readonly ConcurrentDictionary<(string Title, double Size, bool Bold, bool Italic, int Level, bool Meta), (double Width, double Height)> cache = new();
    public static double Width(double naturalWidth, double fontSize, int level)
    {
        var minimum = level == 1 ? 200 : level == 2 ? 170 : 140;
        var maximum = level == 1 ? 280 : level == 2 ? 250 : 220;
        var scale = Math.Max(1, fontSize / 22);
        return Math.Ceiling(Math.Clamp(naturalWidth + 28, minimum * scale, maximum * scale));
    }
    public static double Height(double textHeight, int level, bool metadata) => Math.Ceiling(Math.Max(level == 1 ? 72 : level == 2 ? 64 : 58, 38 + textHeight + (metadata ? 22 : 0)));
    public static bool HasMetadata(ChartNode node) => !string.IsNullOrWhiteSpace(node.Notes) || node.StartDate.HasValue || node.FinishDate.HasValue;
    // Export geometry uses the same bundled font as the PDF writer, never an estimated character width.
    public static (double Width, double Height) Measure(ChartNode node, int level = 1)
    {
        var key = (node.Title, node.Format.Size, node.Format.Bold, node.Format.Italic, level, HasMetadata(node));
        if (cache.Count > 4096) cache.Clear();
        return cache.GetOrAdd(key, _ =>
        {
            ChartExports.EnsureFonts(); using var document = new PdfDocument();
            using var graphics = XGraphics.FromPdfPage(document.AddPage());
            var font = DiagramPdf.Font(node.Format.Size, node.Format.Bold, node.Format.Italic);
            var natural = node.Title.Split('\n').Max(line => graphics.MeasureString(line, font).Width);
            var width = Width(natural, node.Format.Size, level);
            var lines = ChartExports.Wrap(graphics, node.Title, font, width - 28).Count();
            return (width, Height(lines * node.Format.Size * 1.35, level, HasMetadata(node)));
        });
    }
}
