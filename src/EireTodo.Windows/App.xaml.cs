using System.IO;
using System.Globalization;
using System.Windows;
using EireTodo.Core;

namespace EireTodo.Windows;

public partial class App : Application
{
    private FileStream? instanceLock;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            StartupDiagnostics.Report(args.Exception,"An unexpected error occurred. The app will close; changes already saved remain on disk.");
            Shutdown(1);
        };
        CultureInfo.DefaultThreadCurrentCulture = AustralianDates.Culture;
        CultureInfo.DefaultThreadCurrentUICulture = AustralianDates.Culture;
        if (e.Args.Length == 2 && e.Args[0] == "--capture-previews")
        {
            try { await VisualCapture.Run(e.Args[1]); Shutdown(); }
            catch (Exception ex) { MessageBox.Show("Windows visual capture failed: " + ex.Message, "SUMAPP visual capture", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
            return;
        }
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EireTodo");
        try
        {
            Directory.CreateDirectory(directory);
            try { instanceLock = new FileStream(Path.Combine(directory, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException)
            {
                MessageBox.Show("SUMAPP is already running, or its data folder is locked. Close the other instance and try again.", "SUMAPP", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown(); return;
            }
            var store = new DataStore(directory);
            DataDocument data;
            try { data = store.Load(); }
            catch (Exception ex)
            {
                if (File.Exists(store.RecoveryPath) && MessageBox.Show($"The saved data could not be read:\n{ex.Message}\n\nRecover the previous save? Your unreadable file will be kept for investigation. The most recent change may not be in the previous save.", "Data recovery", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    data = store.RecoverPrevious();
                else throw new IOException("The app stopped to protect your saved data. Restore a valid backup or check the data folder.\n" + ex.Message, ex);
            }
            // Verify a writable data file on first run, before accepting edits.
            if (!File.Exists(store.DataPath)) store.Save(data);
            if(!data.OrganisationBrandMigrated&&data.BrandLogoPng.Length>0&&data.Diagrams.Count>0)
            {
                var branded=data.Clone();
                if(OrganisationBranding.MigrateLegacyLogo(branded))
                {
                    try{store.Save(branded);data=branded;}
                    catch(IOException ex){MessageBox.Show("The original SUMAPP brand is restored, but the previous custom logo could not be saved as organisation branding. Your existing profile remains available. Choose a smaller organisation PNG under Format.\n\n"+ex.Message,"Organisation logo not saved",MessageBoxButton.OK,MessageBoxImage.Warning);}
                }
            }
            var window = new MainWindow(new TodoService(store, data), directory);
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Report(ex,"SUMAPP could not start. No new tasks were accepted.");
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e) { instanceLock?.Dispose(); base.OnExit(e); }
}
