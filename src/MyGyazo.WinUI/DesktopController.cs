using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using MyGyazo.Core;
using Forms = System.Windows.Forms;

namespace MyGyazo;

internal sealed class DesktopController : IDisposable
{
    private readonly Application app;
    private readonly SettingsStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyGyazo", "settings.json"));
    private Forms.NotifyIcon? tray;
    private Hotkey? hotkey;
    private bool capturing;
    internal AppSettings Settings { get; private set; }
    internal MainWindow Window { get; private set; } = null!;
    internal bool Exiting { get; private set; }
    internal DesktopController(Application app, AppSettings? settings = null) { this.app = app; Settings = settings ?? AppSettings.Default; }
    internal void Start(bool background)
    {
        string? warning = null;
        try { Settings = store.Load(); } catch (Exception ex) { warning = "Settings could not be loaded. Defaults are in use. " + ex.Message; }
        Window = new MainWindow(this);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Capture", null, (_, _) => _ = Capture());
        menu.Items.Add("Library", null, (_, _) => Show(false));
        menu.Items.Add("Settings", null, (_, _) => Show(true));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        tray = new Forms.NotifyIcon { Text = "My Gyazo", Icon = new Icon(Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico")), ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Show(false);
        tray.BalloonTipClicked += (_, _) => Show(false);
        hotkey = new Hotkey(); hotkey.Pressed += () => _ = Capture();
        if (!hotkey.Register(Settings.Modifiers, Settings.Key)) warning = "The capture shortcut is unavailable. Choose another in Settings; tray capture still works.";
        if (!background) Show(false);
        if (warning is not null) { Notify(warning); Window.SetStatus(warning, true); }
    }
    internal static string ShortcutLabel(AppSettings settings) =>
        ((settings.Modifiers & 2) != 0 ? "Ctrl+" : "") + ((settings.Modifiers & 4) != 0 ? "Shift+" : "") +
        ((settings.Modifiers & 1) != 0 ? "Alt+" : "") + (settings.Key is >= 0x30 and <= 0x39 ? ((char)settings.Key).ToString() : ((Forms.Keys)settings.Key).ToString());
    private void Show(bool settings)
    {
        Window.AppWindow.Show(); Window.Activate();
        if (settings) Window.ShowSettings(); else { Window.ShowLibrary(); _ = Window.Refresh(); }
    }
    internal async Task Capture()
    {
        if (capturing) return;
        capturing = true;
        bool restore = Window.AppWindow.IsVisible;
        if (restore) Window.AppWindow.Hide();
        try
        {
            await Task.Delay(180);
            using var bitmap = await SelectArea();
            if (bitmap is null) return;
            var folder = Settings.CaptureFolder;
            var result = await CaptureCoordinator.Complete(() => Task.Run(() =>
            {
                Directory.CreateDirectory(folder);
                var path = CaptureFiles.NewPath(folder); var temporary = path + ".tmp";
                try
                {
                    using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) bitmap.Save(file, ImageFormat.Png);
                    File.Move(temporary, path); return path;
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }), () => CopyImage(bitmap));
            var message = (result.SaveError is null ? "PNG saved. " : "Save failed: " + result.SaveError + " ") +
                (result.ClipboardError is null ? "Image copied. Ready to paste." : "Clipboard failed: " + result.ClipboardError);
            Notify(message);
            await Window.Refresh();
            Window.SetStatus(message, result.SaveError is not null || result.ClipboardError is not null);
        }
        catch (Exception ex) { Notify("Capture failed: " + ex.Message); Window.SetStatus(ex.Message, true); }
        finally
        {
            capturing = false;
            if (restore && !Exiting) { Window.AppWindow.Show(); Window.Activate(); }
        }
    }
    private static Task<Bitmap?> SelectArea()
    {
        var completion = new TaskCompletionSource<Bitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { using var overlay = new CaptureOverlay(); overlay.ShowDialog(); completion.SetResult(overlay.Result); }
            catch (Exception ex) { completion.SetException(ex); }
        }) { IsBackground = true, Name = "Screenshot selection" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); return completion.Task;
    }
    internal static async Task CopyImage(Bitmap bitmap)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { Forms.Clipboard.SetDataObject(new Forms.DataObject(Forms.DataFormats.Bitmap, bitmap), true, 1, 0); return; }
            catch (System.Runtime.InteropServices.ExternalException) when (attempt < 4) { await Task.Delay(80); }
        }
    }
    internal void SaveSettings(AppSettings proposed)
    {
        if (!Path.IsPathFullyQualified(proposed.CaptureFolder)) throw new InvalidOperationException("Choose an absolute capture folder path.");
        if (proposed.Modifiers == 0) throw new InvalidOperationException("Choose at least one shortcut modifier.");
        // UI verification never initializes system integration or writes preferences.
        if (hotkey is null) throw new InvalidOperationException("Settings cannot be saved in preview mode.");
        using var startup = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        var previousStartup = startup.GetValue("MyGyazo") as string;
        if (!hotkey.Register(proposed.Modifiers, proposed.Key)) throw new InvalidOperationException("That shortcut is already in use. Choose another.");
        try
        {
            if (proposed.LaunchAtLogin) startup.SetValue("MyGyazo", "\"" + Environment.ProcessPath + "\" --background");
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
    }
    internal void Notify(string message) => tray?.ShowBalloonTip(4000, "My Gyazo", message, Forms.ToolTipIcon.Info);
    internal void Exit() { Exiting = true; Dispose(); Window.Close(); app.Exit(); }
    public void Dispose()
    {
        hotkey?.Dispose(); hotkey = null;
        if (tray is not null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Icon?.Dispose(); tray.Dispose(); tray = null; }
    }
}
