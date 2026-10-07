using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace EireTodo.Windows;

internal static class Branding
{
    // Do not silently substitute recreated artwork for the supplied company logo.
    internal static void ApplyEireLogo(Image logo, TextBlock pending)
    {
        var uri = new Uri("/SUMAPP;component/Assets/EireLogo.png", UriKind.Relative);
        try
        {
            if (Application.GetResourceStream(uri) is null) return;
            logo.Source = new BitmapImage(uri); logo.Visibility = Visibility.Visible;
            pending.Visibility = Visibility.Collapsed;
        }
        catch (System.IO.IOException) { /* Awaiting the authoritative logo asset. */ }
    }
}
