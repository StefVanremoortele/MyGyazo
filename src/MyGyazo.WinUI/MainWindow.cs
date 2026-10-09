using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using MyGyazo.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Forms = System.Windows.Forms;

namespace MyGyazo;

public sealed class CaptureEntry : INotifyPropertyChanged
{
    public string Path { get; }
    public string Name => System.IO.Path.GetFileName(Path);
    public DateTime Timestamp { get; }
    public string DisplayDate => Timestamp.Date == DateTime.Today ? "Today" : Timestamp.Date == DateTime.Today.AddDays(-1) ? "Yesterday" : Timestamp.ToString("MMM d, yyyy");
    public string DisplayTime => Timestamp.ToString("HH:mm:ss");
    public string SearchText => Name + " " + DisplayDate + " " + Timestamp.ToString("g");
    public BitmapImage? Thumbnail { get; private set; }
    private bool thumbnailRequested;
    public event PropertyChangedEventHandler? PropertyChanged;
    public CaptureEntry(string path) { Path = path; Timestamp = File.GetLastWriteTime(path); }
    public async Task LoadThumbnail()
    {
        if (thumbnailRequested) return;
        thumbnailRequested = true;
        try { Thumbnail = await LoadImage(Path, 160); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail))); }
        catch { /* Invalid or removed captures can still be selected and deleted. */ }
    }
    internal static async Task<BitmapImage> LoadImage(string path, int width = 0)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenReadAsync();
        var image = new BitmapImage { DecodePixelWidth = width };
        await image.SetSourceAsync(stream); return image;
    }
}

internal sealed class MainWindow : Window
{
    private readonly DesktopController controller;
    internal Grid Root { get; }
    private List<CaptureEntry> captures = [];
    private int refreshVersion, previewVersion;
    private bool changingItems;
    private bool newestFirst = true;
    private bool dialogOpen;
    private readonly WindowSizing sizing;
    internal T Find<T>(string name) where T : class => (T)Root.FindName(name);
    internal MainWindow(DesktopController controller)
    {
        this.controller = controller;
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("MyGyazo.WinUI.Views.Shell.xaml")!;
        using var reader = new StreamReader(stream);
        Root = (Grid)XamlReader.Load(reader.ReadToEnd()); Content = Root;
        Title = "My Gyazo";
        sizing = new WindowSizing(WinRT.Interop.WindowNative.GetWindowHandle(this));
        Closed += (_, _) => sizing.Dispose();
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true; SetTitleBar(Find<Grid>("TitleBar"));
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "App.ico"));
        var scale = Native.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this)) / 96d;
        var working = Forms.Screen.PrimaryScreen!.WorkingArea;
        int width = Math.Min((int)(1160 * scale), working.Width - 60), height = Math.Min((int)(800 * scale), working.Height - 60);
        AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(working.X + (working.Width - width) / 2, working.Y + (working.Height - height) / 2, width, height));
        AppWindow.Closing += (_, e) => { if (!controller.Exiting) { e.Cancel = true; AppWindow.Hide(); } };
        Find<NavigationView>("Navigation").SelectionChanged += (_, e) =>
        {
            if (e.IsSettingsSelected) { LoadSettings(); SetPage(true); } else SetPage(false);
        };
        Find<RadioMenuFlyoutItem>("NewestSortItem").Click += (_, _) => SetSort(true);
        Find<RadioMenuFlyoutItem>("OldestSortItem").Click += (_, _) => SetSort(false);
        Find<Button>("RefreshButton").Click += async (_, _) => await Refresh();
        Find<TextBox>("SearchBox").TextChanged += (_, _) => ApplyFilter();
        Find<ListView>("CaptureList").SelectionChanged += async (_, _) => { if (!changingItems) await UpdatePreview(); };
        Find<ListView>("CaptureList").ContainerContentChanging += async (_, e) => { if (!e.InRecycleQueue && e.Item is CaptureEntry entry) await entry.LoadThumbnail(); };
        Find<AppBarButton>("CopyButton").Click += async (_, _) => await CopySelected();
        Find<AppBarButton>("OpenButton").Click += (_, _) => WithSelected(entry => Process.Start(new ProcessStartInfo(entry.Path) { UseShellExecute = true }));
        Find<AppBarButton>("RevealButton").Click += (_, _) => WithSelected(entry => Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + entry.Path + "\"") { UseShellExecute = true }));
        Find<AppBarButton>("DeleteButton").Click += async (_, _) => await DeleteSelected();
        Find<Button>("BrowseButton").Click += async (_, _) => await Browse();
        Find<Button>("CancelSettingsButton").Click += (_, _) => ShowLibrary();
        Find<Button>("SaveSettingsButton").Click += async (_, _) => await SaveSettings();
        Root.SizeChanged += (_, _) =>
        {
            Find<ColumnDefinition>("LibraryColumn").Width = new GridLength(Root.ActualWidth < 1000 ? 216 : 248);
        };
        ShowLibrary();
        _ = Refresh();
    }
    internal void ShowLibrary() { Find<NavigationView>("Navigation").SelectedItem = Find<NavigationViewItem>("LibraryNavigation"); SetPage(false); }
    internal void ShowSettings() { var nav = Find<NavigationView>("Navigation"); nav.SelectedItem = nav.SettingsItem; LoadSettings(); SetPage(true); }
    private void SetPage(bool settings)
    {
        Find<Grid>("LibraryPage").Visibility = settings ? Visibility.Collapsed : Visibility.Visible;
        Find<Grid>("SettingsPage").Visibility = settings ? Visibility.Visible : Visibility.Collapsed;
    }
    internal async Task Refresh()
    {
        int request = ++refreshVersion;
        try
        {
            var folder = controller.Settings.CaptureFolder;
            var entries = await Task.Run(() => CaptureFiles.List(folder).Select(path => new CaptureEntry(path)).ToList());
            if (request != refreshVersion) return;
            captures = entries; ApplyFilter();
        }
        catch (Exception ex)
        {
            if (request != refreshVersion) return;
            captures = []; ApplyFilter();
            Find<TextBlock>("EmptyTitle").Text = "Folder unavailable";
            Find<TextBlock>("EmptyDescription").Text = "Check the folder in Settings and refresh your library.";
            SetStatus("Cannot read capture folder: " + ex.Message, true);
        }
    }
    internal void SetSort(bool newest)
    {
        newestFirst = newest;
        Find<RadioMenuFlyoutItem>("NewestSortItem").IsChecked = newest;
        Find<RadioMenuFlyoutItem>("OldestSortItem").IsChecked = !newest;
        Find<TextBlock>("SortLabel").Text = newest ? "Newest first" : "Oldest first";
        ApplyFilter();
    }
    private void ApplyFilter()
    {
        var list = Find<ListView>("CaptureList"); var selected = (list.SelectedItem as CaptureEntry)?.Path;
        var query = Find<TextBox>("SearchBox").Text.Trim();
        var matches = captures.Where(entry => entry.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        var ordered = newestFirst ? matches.OrderByDescending(entry => entry.Timestamp) : matches.OrderBy(entry => entry.Timestamp);
        var visible = ordered.ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase).ToList();
        changingItems = true;
        list.ItemsSource = visible; list.SelectedItem = visible.FirstOrDefault(entry => entry.Path == selected) ?? visible.FirstOrDefault();
        changingItems = false;
        Find<TextBlock>("EmptyTitle").Text = query.Length == 0 ? "No captures yet" : "No matching captures";
        Find<TextBlock>("EmptyDescription").Text = query.Length == 0 ? "Press " + DesktopController.ShortcutLabel(controller.Settings) + " to take a screenshot." : "Try another filename or date, or clear your search.";
        _ = UpdatePreview();
    }
    internal async Task UpdatePreview()
    {
        int request = ++previewVersion;
        var entry = Find<ListView>("CaptureList").SelectedItem as CaptureEntry;
        Find<Grid>("PreviewPanel").Visibility = entry is null ? Visibility.Collapsed : Visibility.Visible;
        Find<StackPanel>("EmptyPanel").Visibility = entry is null ? Visibility.Visible : Visibility.Collapsed;
        Find<CommandBar>("ImageActions").IsEnabled = entry is not null;
        Find<Image>("PreviewImage").Source = null; Find<TextBlock>("PreviewError").Text = "";
        if (entry is null) return;
        ToolTipService.SetToolTip(Find<Image>("PreviewImage"), entry.Path);
        try
        {
            var image = await CaptureEntry.LoadImage(entry.Path, 1400);
            if (request != previewVersion) return;
            Find<Image>("PreviewImage").Source = image;
        }
        catch (Exception ex)
        {
            if (request != previewVersion) return;
            Find<TextBlock>("PreviewError").Text = "This image could not be previewed.\n" + ex.Message;
        }
    }
    internal void SetStatus(string text, bool error = false)
    {
        var bar = Find<InfoBar>("StatusBar"); bar.Message = text; bar.Severity = error ? InfoBarSeverity.Error : InfoBarSeverity.Success; bar.IsOpen = true;
    }
    private void WithSelected(Action<CaptureEntry> action)
    {
        if (Find<ListView>("CaptureList").SelectedItem is not CaptureEntry entry) return;
        try { action(entry); } catch (Exception ex) { SetStatus(ex.Message, true); }
    }
    private async Task CopySelected()
    {
        if (Find<ListView>("CaptureList").SelectedItem is not CaptureEntry entry) return;
        try { using var bitmap = new System.Drawing.Bitmap(entry.Path); await DesktopController.CopyImage(bitmap); SetStatus("Image copied. Ready to paste."); }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }
    private async Task DeleteSelected()
    {
        if (dialogOpen || Find<ListView>("CaptureList").SelectedItem is not CaptureEntry entry) return;
        dialogOpen = true;
        try
        {
            var dialog = new ContentDialog { XamlRoot = Root.XamlRoot, Title = "Delete capture?", Content = "This image will move to the Recycle Bin.", PrimaryButtonText = "Delete", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            FileSystem.DeleteFile(entry.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); await Refresh();
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
        finally { dialogOpen = false; }
    }
    private void LoadSettings()
    {
        var settings = controller.Settings;
        Find<TextBox>("FolderBox").Text = settings.CaptureFolder;
        Find<CheckBox>("CtrlBox").IsChecked = (settings.Modifiers & 2) != 0;
        Find<CheckBox>("ShiftBox").IsChecked = (settings.Modifiers & 4) != 0;
        Find<CheckBox>("AltBox").IsChecked = (settings.Modifiers & 1) != 0;
        var keyBox = Find<ComboBox>("KeyBox");
        keyBox.ItemsSource = Enumerable.Range(0x30, 10).Concat(Enumerable.Range(0x41, 26)).Concat(Enumerable.Range(0x70, 12))
            .Select(key => new ShortcutKey((uint)key, key is >= 0x30 and <= 0x39 ? ((char)key).ToString() : ((Forms.Keys)key).ToString())).ToList();
        keyBox.SelectedValue = settings.Key;
        Find<ToggleSwitch>("StartupSwitch").IsOn = settings.LaunchAtLogin;
        Find<InfoBar>("SettingsError").IsOpen = false;
    }
    private async Task Browse()
    {
        try
        {
            var picker = new FolderPicker(); picker.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is not null) Find<TextBox>("FolderBox").Text = folder.Path;
        }
        catch (Exception ex) { SettingsError(ex.Message); }
    }
    internal async Task SaveSettings()
    {
        try
        {
            uint modifiers = (Find<CheckBox>("CtrlBox").IsChecked == true ? 2u : 0) | (Find<CheckBox>("ShiftBox").IsChecked == true ? 4u : 0) | (Find<CheckBox>("AltBox").IsChecked == true ? 1u : 0);
            if (Find<ComboBox>("KeyBox").SelectedValue is not uint key) throw new InvalidOperationException("Choose a shortcut key.");
            var folder = Find<TextBox>("FolderBox").Text.Trim();
            if (!Path.IsPathFullyQualified(folder)) throw new InvalidOperationException("Choose an absolute capture folder path.");
            controller.SaveSettings(new AppSettings(Path.GetFullPath(folder), modifiers, key, Find<ToggleSwitch>("StartupSwitch").IsOn));
            await Refresh(); ShowLibrary(); SetStatus("Your preferences have been saved.");
        }
        catch (Exception ex) { SettingsError(ex.Message); }
    }
    private void SettingsError(string message) { var bar = Find<InfoBar>("SettingsError"); bar.Message = message; bar.IsOpen = true; }
}

public sealed record ShortcutKey(uint Value, string Label);
