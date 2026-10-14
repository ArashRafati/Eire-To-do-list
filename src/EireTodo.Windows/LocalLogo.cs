using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using EireTodo.Core;

namespace EireTodo.Windows;

public partial class MainWindow
{
    private static BitmapImage DecodeLogo(byte[] bytes)
    {
        LogoAsset.Validate(bytes);
        using var stream = new MemoryStream(bytes, false);
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.DecodePixelWidth = 256; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
    private void ApplyChosenLogo()
    {
        SumappLogo.Source = Branding.LoadBundledImage("SumappLogo.png");
        Application.Current.Resources["ProgramIcon"] = Branding.LoadBundledImage("AppIcon.ico");
    }
}
