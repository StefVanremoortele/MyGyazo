using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using MyGyazo.Core;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MyGyazo;

internal static class UiChecks
{
    internal static async Task Run(Application app, string destination)
    {
        var output = Path.GetFullPath(destination); Directory.CreateDirectory(output);
        using var log = new StreamWriter(Path.Combine(output, "checks.txt")) { AutoFlush = true };
        int count = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAIL: " + message);
            log.WriteLine("PASS: " + message); count++;
        }
        try
        {
            var folder = Path.Combine(output, "fixtures-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
            using var controller = new DesktopController(app, new AppSettings(folder));
            var window = new MainWindow(controller);
            window.AppWindow.Move(new Windows.Graphics.PointInt32(-20000, -20000));
            var loaded = new TaskCompletionSource(); window.Root.Loaded += (_, _) => loaded.TrySetResult();
            window.Activate(); await loaded.Task.WaitAsync(TimeSpan.FromSeconds(15));
            window.Root.RequestedTheme = ElementTheme.Light;
            await window.Refresh(); await Task.Delay(250);
            Check(window.Find<StackPanel>("EmptyPanel").Visibility == Visibility.Visible, "Empty library has a capture prompt");
            Check(!window.Find<CommandBar>("ImageActions").IsEnabled, "Image actions disabled without selection");
            await Render(window, Path.Combine(output, "library-empty.png"));
            for (int i = 0; i < 5; i++)
            {
                string path = Path.Combine(folder, $"Capture-example-{i}.png");
                Fixture(path, i); File.SetLastWriteTime(path, DateTime.Today.AddHours(14).AddMinutes(-i * 22));
            }
            await window.Refresh(); await window.UpdatePreview(); await Task.Delay(300);
            var list = window.Find<ListView>("CaptureList");
            Check(list.Items.Count == 5 && list.SelectedIndex == 0, "Newest capture selected automatically");
            Check(window.Find<Microsoft.UI.Xaml.Controls.Image>("PreviewImage").Source is not null, "Native WinUI preview loads the PNG");
            var selected = ((CaptureEntry)list.SelectedItem).Path;
            window.SetSort(false);
            Check(((CaptureEntry)list.Items[0]).Name == "Capture-example-4.png" && ((CaptureEntry)list.SelectedItem).Path == selected, "Oldest-first sorting preserves the selected capture");
            await window.Refresh();
            Check(((CaptureEntry)list.Items[0]).Name == "Capture-example-4.png", "Refresh preserves the chosen sort order");
            var sortSearch = window.Find<TextBox>("SearchBox"); sortSearch.Text = "example"; await Task.Delay(150);
            var dates = list.Items.Cast<CaptureEntry>().Select(entry => entry.Timestamp).ToList();
            Check(dates.SequenceEqual(dates.OrderBy(date => date)), "Search results respect oldest-first sorting");
            sortSearch.Text = ""; await Task.Delay(150);
            window.SetSort(true);
            Check(((CaptureEntry)list.Items[0]).Name == "Capture-example-0.png" && window.Find<TextBlock>("SortLabel").Text == "Newest first", "Newest-first sorting restores the order and button label");
            await window.UpdatePreview();
            await Render(window, Path.Combine(output, "library-light.png"));
            window.Root.RequestedTheme = ElementTheme.Dark; await Task.Delay(200);
            await Render(window, Path.Combine(output, "library-dark.png"));
            var search = window.Find<TextBox>("SearchBox"); search.Text = "example-2"; await Task.Delay(150);
            Check(list.Items.Count == 1, "Search filters captures by filename");
            search.Text = "nothing-matches"; await Task.Delay(150);
            Check(list.Items.Count == 0 && window.Find<StackPanel>("EmptyPanel").Visibility == Visibility.Visible, "No-match state is displayed");
            search.Text = ""; await Task.Delay(150); list.SelectedIndex = 2;
            await window.Refresh();
            Check(list.SelectedIndex == 2, "Refresh preserves selection");
            string corrupt = Path.Combine(folder, "corrupt.png"); File.WriteAllText(corrupt, "not an image"); File.SetLastWriteTime(corrupt, DateTime.Today.AddDays(1));
            await window.Refresh(); list.SelectedIndex = 0; await window.UpdatePreview();
            Check(window.Find<TextBlock>("PreviewError").Text.Length > 0, "Invalid PNG reports a recoverable error");
            File.Delete(corrupt); await window.Refresh();
            Check(list.Items.Count == 5, "Externally removed files disappear after refresh");
            window.ShowSettings();
            Check(window.Find<Grid>("SettingsPage").Visibility == Visibility.Visible, "Navigation opens native settings");
            Check((uint)window.Find<ComboBox>("KeyBox").SelectedValue == 0x32, "Shortcut restored in the selector");
            window.Find<TextBox>("FolderBox").Text = @"C:\Users\You\Pictures\My Gyazo";
            await Render(window, Path.Combine(output, "settings-dark.png"));
            window.Root.RequestedTheme = ElementTheme.Light; await Task.Delay(200);
            await Render(window, Path.Combine(output, "settings-light.png"));
            window.Find<TextBox>("FolderBox").Text = "relative-path"; await window.SaveSettings();
            Check(window.Find<InfoBar>("SettingsError").IsOpen, "Invalid settings use a native inline InfoBar");
            window.ShowLibrary();
            Native.PostMessage(WinRT.Interop.WindowNative.GetWindowHandle(window), 0x0010, IntPtr.Zero, IntPtr.Zero);
            await Task.Delay(200);
            Check(!window.AppWindow.IsVisible, "Closing the window hides it for tray operation");
            log.WriteLine($"All {count} WinUI checks passed."); Environment.ExitCode = 0;
        }
        catch (Exception ex) { log.WriteLine(ex); Environment.ExitCode = 1; }
        finally { app.Exit(); }
    }
    private static async Task Render(MainWindow window, string path)
    {
        await Task.Delay(200);
        var target = new RenderTargetBitmap(); await target.RenderAsync(window.Root);
        var buffer = await target.GetPixelsAsync(); var pixels = new byte[buffer.Length];
        using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(pixels);
        // Mica is a compositor backdrop, outside RenderTargetBitmap. Composite the
        // premultiplied client pixels over its neutral theme fallback for previews.
        byte background = window.Root.ActualTheme == ElementTheme.Dark ? (byte)32 : (byte)243;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            int remaining = 255 - pixels[i + 3];
            for (int channel = 0; channel < 3; channel++) pixels[i + channel] = (byte)Math.Min(255, pixels[i + channel] + background * remaining / 255);
            pixels[i + 3] = 255;
        }
        var file = await StorageFile.GetFileFromPathAsync(CreateFile(path));
        using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)target.PixelWidth, (uint)target.PixelHeight, 96, 96, pixels);
        await encoder.FlushAsync();
    }
    private static string CreateFile(string path) { using var file = File.Create(path); return path; }
    private static void Fixture(string path, int index)
    {
        using var bitmap = new Bitmap(1000, 640); using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.FromArgb(246, 245, 240));
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        using var ink = new SolidBrush(Color.FromArgb(35, 48, 58)); using var accent = new SolidBrush(Color.FromArgb(60 + index * 12, 112, 100));
        using var title = new Font("Segoe UI", 30); using var heading = new Font("Segoe UI", 22); using var body = new Font("Segoe UI", 14); using var small = new Font("Segoe UI", 11);
        graphics.DrawString("FIELDNOTES", body, ink, 50, 32); graphics.DrawString("Journal     Projects     About", small, ink, 690, 36);
        graphics.DrawString("A little space for big ideas.", title, ink, 50, 134);
        graphics.DrawString("Collect your thoughts. Make room for what comes next.", body, ink, 50, 202);
        graphics.FillRectangle(accent, 50, 267, 900, 276);
        graphics.DrawString("WEEK " + (index + 1).ToString("00"), small, Brushes.White, 88, 302);
        graphics.DrawString("Less noise. More possibility.", heading, Brushes.White, 88, 350);
        graphics.DrawString("A collection of small observations and fresh starts.", body, Brushes.White, 88, 406);
        graphics.DrawString("RECENT NOTES", small, ink, 50, 588); bitmap.Save(path, ImageFormat.Png);
    }
}
