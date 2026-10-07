using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using EireTodo.Core;

namespace EireTodo.Windows;

// Windows-only captures of the real WPF interface, with an isolated disposable data folder.
internal static class VisualCapture
{
    internal static async Task Run(string destination)
    {
        destination = Path.GetFullPath(destination); Directory.CreateDirectory(destination);
        var directory = Path.Combine(Path.GetTempPath(), "SUMAPP-visual-checks-" + Guid.NewGuid().ToString("N"));
        var previousDirectory = Directory.GetCurrentDirectory();
        MainWindow? window = null;
        try
        {
            var store = new DataStore(directory); var service = new TodoService(store, store.Load());
            var project = service.Data.Projects[0].Id; service.RenameProject(project, "SOU001 · Mechanical items");
            var titles = new[] { "Steel fabrication BOQ", "Guide rail", "Chain hooks", "Chain supports", "Pipe supports", "DN100 Stub Pipe vent from Drain Valve — stainless steel fabrication and supports" };
            for (var i = 0; i < titles.Length; i++) service.SaveTask(new() { ProjectId = project, Description = titles[i], Category = i == 0 ? "Procurement" : "Fabrication", Notes = "Review drawing and confirm dimensions.\nRecord engineering comments here.", Priority = i == 0, Completed = i == 1, FinishDate = DateOnly.FromDateTime(DateTime.Today.AddDays(i == 2 ? -2 : 4 + i)) });
            var chart = Charts.Create("SOU001 · Mechanical items", ChartLayout.MindMap, project);
            var fabrication = Charts.Add(chart, chart.Nodes[0].Id, true, "Fabrication");
            var procurement = Charts.Add(chart, chart.Nodes[0].Id, true, "Procurement"); procurement.Priority = true;
            foreach (var title in titles.Skip(1)) Charts.Add(chart, fabrication.Id, true, title);
            Charts.Add(chart, procurement.Id, true, titles[0]); service.SaveDiagram(chart);
            var settings = service.Data.Settings; settings.Width = 1280; settings.Height = 850; settings.Opacity = 1; settings.SelectedDiagramId = chart.Id; service.SaveSettings(settings);
            // Exercise resource loading without any Assets folder beside the working directory.
            Directory.SetCurrentDirectory(directory);
            window = new MainWindow(service, directory); Application.Current.MainWindow = window; window.Show();
            Assert(window.Icon is BitmapSource icon && icon.PixelWidth > 0 && icon.PixelHeight > 0,"Bundled startup icon failed to decode");
            Assert(((Image)window.FindName("SumappLogo")).Source is BitmapSource brand && brand.PixelWidth > 0,"Bundled startup logo failed to decode");
            Assert(((Image)window.FindName("EireLogo")).Source is BitmapSource company && company.PixelWidth > 0,"Bundled Eire logo failed to decode");
            var logo = Application.GetResourceStream(Branding.BundledUri("SumappLogo.png"))!;
            using (logo.Stream) using (var original = new MemoryStream()) { logo.Stream.CopyTo(original); window.CaptureLogo(original.ToArray()); }
            Assert(ReferenceEquals(((Image)window.FindName("SumappLogo")).Source,window.Icon),"Chosen logo and window icon differ");
            await Settle(window); Save(window, Path.Combine(destination, "SUMAPP-to-do.png"));
            window.CaptureMode(AppMode.Diagram); await Settle(window); Save(window, Path.Combine(destination, "SUMAPP-mind-map.png"));
            var workspace = Descendants<ChartWorkspace>(window).Single(); workspace.CaptureRibbon("Format"); await Settle(window); Save(window, Path.Combine(destination, "SUMAPP-diagram-gallery.png"));
            Assert(Descendants<DiagramThumbnail>(window).Count(t => t.IsVisible && t.ActualWidth > 0 && t.ActualHeight > 0) >= 6, "Diagram gallery previews missing");
            workspace.CaptureRibbon("View"); await Settle(window); Save(window, Path.Combine(destination, "SUMAPP-layout-gallery.png"));
            var eire = (Image)window.FindName("EireLogo"); Assert(eire.Source is not null && eire.IsVisible, "Eire header artwork missing");
            var results = new List<string> { "Native Windows WPF captures; isolated sample data; current user account.", "To-do, mind-map, Format gallery and View layout images captured at 1280 × 850 logical pixels.", "PASS startup and decoded bundled ICO / SUMAPP / Eire artwork from an empty working folder before selecting a custom logo.", "PASS visible gallery thumbnails and bundled Eire header logo." };
            foreach (var size in new[] { new Size(520, 400), new Size(940, 620), new Size(1440, 900) })
            {
                window.Width = size.Width; window.Height = size.Height; await Settle(window);
                var mode = (ComboBox)window.FindName("ModeSelector"); var clock = (TextBlock)window.FindName("HeaderClock");
                Assert(mode.ActualWidth > 0 && clock.ActualWidth > 0 && clock.TransformToAncestor(window).Transform(new Point(clock.ActualWidth, 0)).X <= window.ActualWidth + 1, "Module / footer clipped at " + size);
                Assert(Descendants<Button>(window).Any(b => b.IsVisible && System.Windows.Automation.AutomationProperties.GetName(b) == "View") || Descendants<Button>(window).Any(b => b.IsVisible && Equals(b.Content, "View")), "View tab hidden at " + size);
                var close = (Button)window.FindName("CloseWindowButton"); var edge = close.TransformToAncestor(window).Transform(new Point(close.ActualWidth,0));
                Assert(Math.Abs(edge.X - window.ActualWidth) <= 2,"Caption controls are not at the right edge");
                Assert(Math.Abs(clock.FontSize - 15.6) < .01 && clock.TransformToAncestor(window).Transform(new Point(0,0)).Y > window.ActualHeight - 65,"Clock is not in the bottom footer");
                results.Add("PASS module / footer / View tab / caption edge at " + size);
            }
            foreach (var font in new[] { "Segoe UI", "Arial", "Consolas" }) foreach (var size in new[] { 12d, 17, 32, 48 })
            {
                var node = new ChartNode { Title = titles[^1], Notes = "Optional metadata", Format = new() { Family = font, Size = size, Bold = true } };
                var dimensions = WindowsNodeMetrics.Measure(node, 3);
                var text = new TextBlock { Text = node.Title, TextWrapping = TextWrapping.Wrap, LineHeight = size * 1.35, LineStackingStrategy = LineStackingStrategy.BlockLineHeight }; FormattingTools.Apply(text, node.Format);
                text.Measure(new Size(dimensions.Width - 28, double.PositiveInfinity));
                Assert(text.DesiredSize.Height + 38 + 22 <= dimensions.Height + .01, "Node title clipped: " + font + " " + size);
            }
            results.Add("PASS long-title sizing at 12–48 px, bold, metadata, Segoe UI / Arial / Consolas.");
            File.WriteAllLines(Path.Combine(destination, "visual-checks.txt"), results);
        }
        finally { window?.Close(); Directory.SetCurrentDirectory(previousDirectory); if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }
    private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static async Task Settle(Window window) { await Task.Delay(350); await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle); }
    private static void Save(Window window, string path)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth), (int)Math.Ceiling(window.ActualHeight), 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var output = File.Create(path); encoder.Save(output);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T match) yield return match; foreach (var descendant in Descendants<T>(child)) yield return descendant; }
    }
}
