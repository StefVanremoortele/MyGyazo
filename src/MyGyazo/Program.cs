using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using MyGyazo.Core;
using Forms = System.Windows.Forms;

namespace MyGyazo;

internal sealed class Program : Application
{
    private readonly SettingsStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyGyazo", "settings.json"));
    internal AppSettings Settings { get; private set; } = AppSettings.Default;
    private Forms.NotifyIcon tray = null!;
    private Hotkey hotkey = null!;
    private bool capturing;
    private HistoryWindow? history;
    private SettingsWindow? settingsWindow;

    private readonly bool previewOnly;
    internal Program(AppSettings? initialSettings = null)
    {
        previewOnly = initialSettings is not null;
        Settings = initialSettings ?? AppSettings.Default;
    }

    [STAThread]
    public static void Main()
    {
        using var mutex = new Mutex(true, "Local\\MyGyazo.Desktop", out bool first);
        if (!first) { MessageBox.Show("My Gyazo is already running in the system tray.", "My Gyazo"); return; }
        new Program { ShutdownMode = ShutdownMode.OnExplicitShutdown }.Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        if (previewOnly) return;
        Ui.ApplyTheme(this);
        base.OnStartup(e);
        string? warning = null;
        try { Settings = store.Load(); } catch (Exception ex) { warning = "Settings could not be loaded. Defaults are in use. " + ex.Message; }
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Capture", null, (_, _) => _ = Capture());
        menu.Items.Add("History", null, (_, _) => ShowHistory());
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Shutdown());
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/MyGyazo;component/Assets/App.ico")).Stream;
        using var sourceIcon = new System.Drawing.Icon(iconStream);
        tray = new Forms.NotifyIcon { Text = "My Gyazo", Icon = (System.Drawing.Icon)sourceIcon.Clone(), ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowHistory();
        tray.BalloonTipClicked += (_, _) => ShowHistory();
        hotkey = new Hotkey();
        hotkey.Pressed += () => _ = Capture();
        if (!hotkey.Register(Settings.Modifiers, Settings.Key)) warning = "The capture shortcut is unavailable. Choose another in Settings; tray capture still works.";
        Notify(warning ?? "Ready. Use " + ShortcutLabel(Settings) + " to capture, or right-click the tray icon.");
    }

    internal static string ShortcutLabel(AppSettings settings) =>
        ((settings.Modifiers & 2) != 0 ? "Ctrl+" : "") + ((settings.Modifiers & 4) != 0 ? "Shift+" : "") +
        ((settings.Modifiers & 1) != 0 ? "Alt+" : "") + ((Forms.Keys)settings.Key).ToString().Replace("D", settings.Key is >= 0x30 and <= 0x39 ? "" : "D");

    internal async Task Capture()
    {
        if (capturing) return;
        capturing = true;
        try
        {
            // Let the tray menu disappear before freezing the screen.
            await Task.Delay(180);
            using var overlay = new CaptureOverlay();
            overlay.ShowDialog();
            using var bitmap = overlay.Result;
            if (bitmap is null) return;
            var folder = Settings.CaptureFolder;
            var result = await CaptureCoordinator.Complete(async () =>
            {
                return await Task.Run(() =>
                {
                    Directory.CreateDirectory(folder);
                    var path = CaptureFiles.NewPath(folder);
                    var temporary = path + ".tmp";
                    try
                    {
                        using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) bitmap.Save(stream, ImageFormat.Png);
                        File.Move(temporary, path);
                        return path;
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                });
            }, () =>
            {
                using var stream = new MemoryStream();
                bitmap.Save(stream, ImageFormat.Png); stream.Position = 0;
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
                return CopyImage(image);
            });
            Notify((result.SaveError is null ? "PNG saved. " : "Save failed: " + result.SaveError + " ") +
                   (result.ClipboardError is null ? "Image copied." : "Clipboard failed: " + result.ClipboardError));
            history?.Refresh();
        }
        catch (Exception ex) { Notify("Capture failed: " + ex.Message); }
        finally { capturing = false; }
    }

    internal static async Task CopyImage(BitmapSource image)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { Clipboard.SetDataObject(new DataObject(DataFormats.Bitmap, image), true); return; }
            catch (System.Runtime.InteropServices.COMException) when (attempt < 4) { await Task.Delay(80); }
        }
    }
    internal void Notify(string message) => tray.ShowBalloonTip(4000, "My Gyazo", message, Forms.ToolTipIcon.Info);
    private void ShowHistory()
    {
        if (history is null) { history = new HistoryWindow(this); history.Closed += (_, _) => history = null; }
        history.Show(); history.Activate();
    }
    internal void ShowSettings()
    {
        if (settingsWindow is null) { settingsWindow = new SettingsWindow(this); settingsWindow.Closed += (_, _) => settingsWindow = null; }
        settingsWindow.Show(); settingsWindow.Activate();
    }
    internal void SaveSettings(AppSettings proposed)
    {
        if (!hotkey.Register(proposed.Modifiers, proposed.Key)) throw new InvalidOperationException("That shortcut is already in use. Choose another.");
        using var startup = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        var previousStartup = startup.GetValue("MyGyazo") as string;
        try
        {
            if (proposed.LaunchAtLogin) startup.SetValue("MyGyazo", "\"" + Environment.ProcessPath + "\"");
            else startup.DeleteValue("MyGyazo", false);
            store.Save(proposed);
        }
        catch
        {
            hotkey.Register(Settings.Modifiers, Settings.Key);
            if (previousStartup is null) startup.DeleteValue("MyGyazo", false); else startup.SetValue("MyGyazo", previousStartup);
            throw;
        }
        Settings = proposed;
        history?.Refresh();
    }
    internal static void OpenFile(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    protected override void OnExit(ExitEventArgs e)
    {
        hotkey?.Dispose();
        if (tray is not null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Icon?.Dispose(); tray.Dispose(); }
        base.OnExit(e);
    }
}
