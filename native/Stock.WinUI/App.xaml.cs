using Microsoft.UI.Xaml;
using Stock.Core;

namespace Stock.WinUI;
public partial class App : Application
{
    private MainWindow? window;
    private Mutex? single;
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => Log(e.Exception);
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        single = new Mutex(true, @"Local\QingdaoIphoneStock.WinUI3", out var first);
        if (!first) { NativeTray.ActivateExisting(); Exit(); return; }
        try
        {
            window = new MainWindow();
            if (Environment.GetCommandLineArgs().Contains("--startup")) window.StartInBackground();
            else window.Activate();
        }
        catch (Exception ex) { Log(ex); throw; }
    }
    internal static void Log(Exception ex)
    {
        try { Directory.CreateDirectory(SettingsStore.DefaultDirectory); File.AppendAllText(Path.Combine(SettingsStore.DefaultDirectory, "errors.log"), $"{DateTimeOffset.Now:O} {ex}\n"); } catch { }
    }
}
