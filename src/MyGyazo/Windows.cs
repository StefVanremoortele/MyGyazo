using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using MyGyazo.Core;
using Forms = System.Windows.Forms;

namespace MyGyazo;

internal static class Ui
{
    internal static void ApplyTheme(Application app) => app.Resources.MergedDictionaries.Add(
        new ResourceDictionary { Source = new Uri("/MyGyazo;component/Theme.xaml", UriKind.Relative) });
    internal static void Error(Exception ex) => MessageBox.Show(ex.Message, "My Gyazo", MessageBoxButton.OK, MessageBoxImage.Warning);
}

internal sealed class CaptureItem
{
    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public DateTime Timestamp { get; }
    public string DisplayDate => Timestamp.Date == DateTime.Today ? "Today" : Timestamp.Date == DateTime.Today.AddDays(-1) ? "Yesterday" : Timestamp.ToString("MMM d, yyyy");
    public string DisplayTime => Timestamp.ToString("HH:mm:ss");
    public string SearchText => Name + " " + DisplayDate + " " + Timestamp.ToString("g");
    public CaptureItem(string path) { Path = path; Timestamp = File.GetLastWriteTime(path); }
    private BitmapImage? thumbnail;
    public BitmapImage? Thumbnail
    {
        get
        {
            if (thumbnail is not null) return thumbnail;
            try { thumbnail = Load(Path, 160); } catch { /* Removed or invalid PNGs remain manageable from history. */ }
            return thumbnail;
        }
    }
    internal static BitmapImage Load(string path, int width = 0)
    {
        using var stream = File.OpenRead(path);
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = width; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
}

internal sealed partial class HistoryWindow : Window
{
    private readonly Program app;
    private List<CaptureItem> captures = [];
    private bool initialized;
    internal HistoryWindow(Program app)
    {
        this.app = app;
        InitializeComponent();
        initialized = true;
        Activated += (_, _) => Refresh();
        Refresh();
    }
    private void WithSelected(Action<CaptureItem> action)
    {
        if (CaptureList.SelectedItem is not CaptureItem item) return;
        try { action(item); } catch (Exception ex) { Ui.Error(ex); }
    }
    internal void Refresh()
    {
        ShortcutHint.Text = Program.ShortcutLabel(app.Settings) + "  ·  capture from anywhere";
        try
        {
            captures = CaptureFiles.List(app.Settings.CaptureFolder).Select(path => new CaptureItem(path)).ToList();
            ApplyFilter();
            StatusLabel.Text = app.Settings.CaptureFolder;
            StatusLabel.ToolTip = app.Settings.CaptureFolder;
        }
        catch (Exception ex)
        {
            captures = []; ApplyFilter();
            EmptyTitle.Text = "Folder unavailable";
            EmptyDescription.Text = "Check your capture folder in Settings, then refresh the library.";
            StatusLabel.Text = "Cannot read capture folder: " + ex.Message;
        }
    }
    private void ApplyFilter()
    {
        var selected = (CaptureList.SelectedItem as CaptureItem)?.Path;
        var query = SearchBox.Text.Trim();
        var visible = captures.Where(item => item.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        CaptureList.ItemsSource = visible;
        CaptureList.SelectedItem = visible.FirstOrDefault(item => item.Path == selected) ?? visible.FirstOrDefault();
        CountLabel.Text = query.Length == 0 ? captures.Count.ToString() : $"{visible.Count} / {captures.Count}";
        EmptyTitle.Text = query.Length == 0 ? "Your first capture starts here" : "No matching captures";
        EmptyDescription.Text = query.Length == 0 ? "Capture a detail, an idea, or something you want to come back to. It stays on your computer." : "Try a different filename or date, or clear the search to see everything.";
        EmptyCaptureButton.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        UpdatePreview();
    }
    private void UpdatePreview()
    {
        var item = CaptureList.SelectedItem as CaptureItem;
        PreviewPanel.Visibility = item is null ? Visibility.Collapsed : Visibility.Visible;
        EmptyPanel.Visibility = item is null ? Visibility.Visible : Visibility.Collapsed;
        SelectionActions.IsEnabled = item is not null;
        PreviewImage.Source = null; PreviewError.Text = "";
        if (item is null) return;
        PreviewTitle.Text = item.DisplayDate + " at " + item.DisplayTime;
        FileLabel.Text = item.Name; FileLabel.ToolTip = item.Path;
        try
        {
            var image = CaptureItem.Load(item.Path, 1400);
            PreviewImage.Source = image;
            var size = new FileInfo(item.Path).Length;
            PreviewDetails.Text = "PNG image  ·  " + (size >= 1048576 ? $"{size / 1048576d:0.#} MB" : $"{Math.Max(1, size / 1024d):0} KB");
        }
        catch (Exception ex) { PreviewError.Text = "This image could not be previewed.\n" + ex.Message; PreviewDetails.Text = "Preview unavailable"; }
    }
    private void Search_Changed(object sender, TextChangedEventArgs e) { if (initialized) ApplyFilter(); }
    private void Selection_Changed(object sender, SelectionChangedEventArgs e) { if (initialized) UpdatePreview(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void Settings_Click(object sender, RoutedEventArgs e) => app.ShowSettings();
    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        Hide(); try { await app.Capture(); } finally { Show(); Activate(); }
    }
    private async void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (CaptureList.SelectedItem is not CaptureItem item) return;
        try { await Program.CopyImage(CaptureItem.Load(item.Path)); StatusLabel.Text = "Image copied. Ready to paste."; } catch (Exception ex) { Ui.Error(ex); }
    }
    private void Open_Click(object sender, RoutedEventArgs e) => WithSelected(item => Program.OpenFile(item.Path));
    private void Capture_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && ItemsControl.ContainerFromElement(CaptureList, source) is ListBoxItem)
            WithSelected(item => Program.OpenFile(item.Path));
    }
    private void Reveal_Click(object sender, RoutedEventArgs e) => WithSelected(item =>
        Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + item.Path + "\"") { UseShellExecute = true }));
    private void Delete_Click(object sender, RoutedEventArgs e) => WithSelected(item =>
    {
        if (MessageBox.Show(this, "Move this capture to the Recycle Bin?", "Delete capture", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        FileSystem.DeleteFile(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); Refresh();
    });
}

internal sealed partial class SettingsWindow : Window
{
    private readonly Program app;
    internal SettingsWindow(Program app)
    {
        this.app = app;
        InitializeComponent();
        FolderBox.Text = app.Settings.CaptureFolder;
        CtrlBox.IsChecked = (app.Settings.Modifiers & 2) != 0;
        ShiftBox.IsChecked = (app.Settings.Modifiers & 4) != 0;
        AltBox.IsChecked = (app.Settings.Modifiers & 1) != 0;
        KeyBox.ItemsSource = Enumerable.Range(0x30, 10).Concat(Enumerable.Range(0x41, 26)).Concat(Enumerable.Range(0x70, 12))
            .Select(value => new { Value = (uint)value, Label = value is >= 0x30 and <= 0x39 ? ((char)value).ToString() : ((Forms.Keys)value).ToString() }).ToList();
        KeyBox.SelectedValue = app.Settings.Key;
        StartupBox.IsChecked = app.Settings.LaunchAtLogin;
    }
    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog { InitialDirectory = FolderBox.Text };
        if (dialog.ShowDialog() == Forms.DialogResult.OK) FolderBox.Text = dialog.SelectedPath;
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            uint modifiers = (CtrlBox.IsChecked == true ? 2u : 0) | (ShiftBox.IsChecked == true ? 4u : 0) | (AltBox.IsChecked == true ? 1u : 0);
            if (modifiers == 0 || KeyBox.SelectedValue is not uint chosen) throw new InvalidOperationException("Choose a key and at least one modifier.");
            if (!Path.IsPathFullyQualified(FolderBox.Text.Trim())) throw new InvalidOperationException("Choose an absolute capture folder path.");
            app.SaveSettings(new AppSettings(Path.GetFullPath(FolderBox.Text.Trim()), modifiers, chosen, StartupBox.IsChecked == true)); Close();
        }
        catch (Exception ex) { SettingsError.Text = ex.Message; SettingsError.Visibility = Visibility.Visible; }
    }
}
