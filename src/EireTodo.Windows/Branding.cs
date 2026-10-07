using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace EireTodo.Windows;

internal static class Branding
{
    // Compact emblem prepared from the supplied inline artwork; the workspace label is UI text.
    internal static void ApplyEireLogo(Image logo, TextBlock pending)
    {
        var uri = new Uri("/SUMAPP;component/Assets/EireLogo.png", UriKind.Relative);
        try
        {
            if (Application.GetResourceStream(uri) is null) return;
            logo.Source = new BitmapImage(uri); logo.Visibility = Visibility.Visible;
            pending.Text = "Eire";
        }
        catch (System.IO.IOException) { pending.Text = "Eire · logo unavailable"; }
    }
}
