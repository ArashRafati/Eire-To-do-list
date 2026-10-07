using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace EireTodo.Windows;

internal static class Branding
{
    internal static Uri BundledUri(string name) => new("pack://application:,,,/SUMAPP;component/Assets/" + name, UriKind.Absolute);

    // Decode embedded bytes eagerly. BitmapImage(relativeUri) can resolve against CWD
    // when constructed outside XAML and turn the assembly resource name into a file path.
    internal static BitmapSource LoadBundledImage(string name)
    {
        var resource = Application.GetResourceStream(BundledUri(name))
            ?? throw new IOException("The bundled image is missing: " + name);
        using var stream = resource.Stream;
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).First();
        frame.Freeze(); return frame;
    }

    // Compact emblem prepared from the supplied inline artwork; the workspace label is UI text.
    internal static void ApplyEireLogo(Image logo, TextBlock pending)
    {
        logo.Source = LoadBundledImage("EireLogo.png"); logo.Visibility = Visibility.Visible;
        pending.Text = "Eire";
    }
}
