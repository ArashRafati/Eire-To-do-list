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
        // Set bundled defaults from streams before applying an optional user image.
        SumappLogo.Source = Branding.LoadBundledImage("SumappLogo.png");
        Application.Current.Resources["ProgramIcon"] = Branding.LoadBundledImage("AppIcon.ico");
        try
        {
            if (service.Data.BrandLogoPng.Length > 0)
            {
                var bitmap = DecodeLogo(service.Data.BrandLogoPng); SumappLogo.Source = bitmap; Application.Current.Resources["ProgramIcon"] = bitmap;
            }
        }
        catch (Exception ex) { MessageBox.Show(this,"The saved logo could not be displayed. Your tasks remain available. Choose a valid PNG to replace it.\n\n" + ex.Message,"Logo unavailable",MessageBoxButton.OK,MessageBoxImage.Warning); }
    }
    internal void CaptureLogo(byte[] bytes) { _ = DecodeLogo(bytes); service.Change(d => d.BrandLogoPng = bytes); ApplyChosenLogo(); }
    private void ChooseLogo()
    {
        if (!chartWorkspace.FinishInlineEdit()) return;
        var dialog = new OpenFileDialog { Title = "Choose your original SUMAPP PNG logo", Filter = "PNG logo (*.png)|*.png", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        if (TryAction(() =>
        {
            if (new FileInfo(dialog.FileName).Length > LogoAsset.MaxBytes) throw new ArgumentException("Choose a PNG up to 4 MB.");
            var bytes = File.ReadAllBytes(dialog.FileName); _ = DecodeLogo(bytes);
            service.Change(d => d.BrandLogoPng = bytes);
        }))
        {
            ApplyChosenLogo();
            MessageBox.Show(this,"Original PNG saved with your data. The header and window/taskbar icons now use it, including after restart.\n\nThe executable's Explorer icon is compiled into the program. Updating that icon requires rebuilding with the original PNG.","Logo saved",MessageBoxButton.OK,MessageBoxImage.Information);
        }
    }
}
