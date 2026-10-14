using System.IO;
using System.Windows;

namespace EireTodo.Windows;

// App.InitializeComponent loads resources before OnStartup and its handlers.
// Catch failures here as well so a resource error cannot exit without a message.
internal static class Startup
{
    [STAThread]
    public static int Main()
    {
        try
        {
            var app = new App();
            app.InitializeComponent();
            return app.Run();
        }
        catch (Exception ex)
        {
            StartupDiagnostics.Report(ex,"The application interface could not load.");
            return 1;
        }
    }
}

internal static class StartupDiagnostics
{
    internal static void Report(Exception error,string context)
    {
        var directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EireTodo");
        var path=Path.Combine(directory,"startup-error.txt");
        string report;
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path,$"SUMAPP {typeof(App).Assembly.GetName().Version}\n{DateTimeOffset.Now:O}\n{context}\n{error}\n");
            report="Error details: "+path;
        }
        catch { report="The error report could not be saved.\n"+error.GetBaseException().Message; }
        MessageBox.Show($"{context}\n\n{error.GetBaseException().Message}\n\nSaved tasks and diagrams have not been deleted.\nData folder: {directory}\n{report}","SUMAPP startup error",MessageBoxButton.OK,MessageBoxImage.Error);
    }
}
