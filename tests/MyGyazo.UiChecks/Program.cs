using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MyGyazo;
using MyGyazo.Core;

internal static class UiChecks
{
    private static int checks;
    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }
    private static void Run(string[] args)
    {
        string output = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/ui-preview");
        string fixture = Path.Combine(output, "fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixture);
        var app = new MyGyazo.Program(new AppSettings(fixture)) { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Ui.ApplyTheme(app);
        var history = new HistoryWindow(app);
        Render(history, Path.Combine(output, "history-empty.png"), 1064, 660);
        Check(((FrameworkElement)history.FindName("EmptyPanel")).Visibility == Visibility.Visible, "Empty library has a useful empty state");
        Check(!((FrameworkElement)history.FindName("SelectionActions")).IsEnabled, "Actions disabled without a capture");
        for (int index = 0; index < 5; index++)
        {
            var path = Path.Combine(fixture, $"Capture-example-{index}.png");
            CreateFixture(path, index);
            File.SetLastWriteTime(path, DateTime.Today.AddHours(14).AddMinutes(-index * 22));
        }
        history.Refresh();
        Render(history, Path.Combine(output, "history.png"), 1064, 660);
        var list = (ListBox)history.FindName("CaptureList");
        Check(list.Items.Count == 5 && list.SelectedIndex == 0, "Newest capture selected automatically");
        Check(((Image)history.FindName("PreviewImage")).Source is not null, "Selected capture renders a preview");
        var search = (TextBox)history.FindName("SearchBox");
        search.Text = "example-2";
        Check(list.Items.Count == 1, "Search filters filenames");
        search.Text = "no-match";
        Check(list.Items.Count == 0 && ((FrameworkElement)history.FindName("EmptyPanel")).Visibility == Visibility.Visible, "Search with no results shows empty state");
        search.Text = "";
        list.SelectedIndex = 2;
        history.Refresh();
        Check(list.SelectedIndex == 2, "Refresh preserves selected capture");
        Render(history, Path.Combine(output, "history-compact.png"), 804, 490);
        File.WriteAllText(Path.Combine(fixture, "broken.png"), "not a PNG");
        File.SetLastWriteTime(Path.Combine(fixture, "broken.png"), DateTime.Today.AddDays(1));
        history.Refresh(); list.SelectedIndex = 0;
        Check(((TextBlock)history.FindName("PreviewError")).Text.Length > 0, "Invalid images show a recoverable preview error");
        var settings = new SettingsWindow(app);
        ((TextBox)settings.FindName("FolderBox")).Text = @"C:\Users\You\Pictures\My Gyazo";
        Render(settings, Path.Combine(output, "settings.png"), 608, 652);
        Check((uint)((ComboBox)settings.FindName("KeyBox")).SelectedValue == 0x32, "Shortcut key restored into Fluent selector");
        ((TextBox)settings.FindName("FolderBox")).Text = "relative-path";
        FindButton((DependencyObject)settings.Content, "Save changes")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(((TextBlock)settings.FindName("SettingsError")).Visibility == Visibility.Visible, "Invalid settings show inline validation");
        Render(settings, Path.Combine(output, "settings-compact.png"), 508, 452);
        history.Close(); settings.Close(); app.Shutdown();
        Console.WriteLine($"All {checks} UI checks passed. Rendered previews: {output}");
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception("FAIL: " + label);
        checks++; Console.WriteLine("PASS: " + label);
    }
    private static Button? FindButton(DependencyObject root, string content)
    {
        if (root is Button button && button.Content as string == content) return button;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var result = FindButton(VisualTreeHelper.GetChild(root, i), content); if (result is not null) return result; }
        return null;
    }
    private static void Render(Window window, string path, int width, int height)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000; window.Top = -20000; window.ShowActivated = false; window.ShowInTaskbar = false;
        window.Width = width + 56; window.Height = height + 80;
        if (!window.IsVisible) window.Show();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        var content = (FrameworkElement)window.Content;
        // Render the actual window after native layout, without activating it.
        window.UpdateLayout();
        width = (int)content.ActualWidth; height = (int)content.ActualHeight;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) { dc.DrawRectangle(window.Background, null, new Rect(0, 0, width, height)); dc.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, width, height)); }
        Save(visual, path, width, height);
    }
    private static void Save(Visual visual, string path, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void CreateFixture(string path, int index)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var ink = new SolidColorBrush(Color.FromRgb(35, 48, 58));
            var accent = new SolidColorBrush(Color.FromRgb((byte)(60 + index * 12), 112, 100));
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(246, 245, 240)), null, new Rect(0, 0, 1000, 640));
            void Text(string value, double size, double x, double y, Brush brush) => dc.DrawText(new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, brush, 1), new Point(x, y));
            Text("FIELDNOTES", 18, 50, 32, ink); Text("Journal     Projects     About", 14, 690, 36, ink);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(216, 221, 216)), 1), new Point(50, 82), new Point(950, 82));
            Text("A little space for big ideas.", 42, 50, 134, ink);
            Text("Collect your thoughts. Make room for what comes next.", 19, 50, 202, ink);
            dc.DrawRoundedRectangle(accent, null, new Rect(50, 267, 900, 276), 14, 14);
            Text("WEEK " + (index + 1).ToString("00"), 15, 88, 302, Brushes.White);
            Text("Less noise. More possibility.", 32, 88, 350, Brushes.White);
            Text("A collection of small observations and fresh starts.", 18, 88, 406, Brushes.White);
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(88, 459, 128, 42), 6, 6);
            Text("Read the journal", 14, 100, 469, ink);
            Text("RECENT NOTES", 13, 50, 588, ink);
        }
        Save(visual, path, 1000, 640);
    }
}
