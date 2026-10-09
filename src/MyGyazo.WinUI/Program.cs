using System;
using System.IO;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Forms = System.Windows.Forms;

namespace MyGyazo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex)
        {
            Report(args, ex.ToString()); Environment.ExitCode = 1;
            if (args.Length == 0 || args[0] != "--ui-checks") Forms.MessageBox.Show("My Gyazo could not start. " + ex.Message, "My Gyazo");
        }
    }
    internal static void Report(string[] args, string message)
    {
        string folder = args.Length > 1 && args[0] == "--ui-checks" ? Path.GetFullPath(args[1]) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyGyazo");
        Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "winui-startup.log"), message + Environment.NewLine);
    }
    private static void Run(string[] args)
    {
        if (args.Length > 0 && args[0] == "--ui-checks") Report(args, "Starting WinUI verification");
        bool check = args.Length > 0 && args[0] == "--ui-checks";
        using var mutex = new Mutex(true, check ? "Local\\MyGyazo.WinUI.Checks" : "Local\\MyGyazo.Desktop", out bool first);
        if (!first) { Forms.MessageBox.Show("My Gyazo is already running. Exit it from the tray before opening this version.", "My Gyazo"); return; }
        Forms.WindowsFormsSynchronizationContext.AutoInstall = false;
        WinRT.ComWrappersSupport.InitializeComWrappers();
        if (check) Report(args, "COM initialized");
        Application.Start(initialization =>
        {
            if (check) Report(args, "WinUI dispatcher initialized");
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App(args);
        });
    }
}

public sealed partial class App : Application
{
    private readonly string[] args;
    private DesktopController? controller;
    internal App(string[] args)
    {
        this.args = args;
        UnhandledException += (_, e) => { Program.Report(args, e.Exception.ToString()); e.Handled = true; Environment.ExitCode = 1; Exit(); };
        InitializeComponent();
    }
    protected override async void OnLaunched(LaunchActivatedEventArgs launch)
    {
        if (args.Length > 0 && args[0] == "--ui-checks")
        {
            await UiChecks.Run(this, args.Length > 1 ? args[1] : "artifacts/winui-preview");
            return;
        }
        controller = new DesktopController(this);
        controller.Start(Array.IndexOf(args, "--background") >= 0);
    }
}
