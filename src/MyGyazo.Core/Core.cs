using System.Text.Json;

namespace MyGyazo.Core;

public record AppSettings(string CaptureFolder, uint Modifiers = 6, uint Key = 0x32, bool LaunchAtLogin = false)
{
    public static AppSettings Default => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "My Gyazo"));
}

public sealed class SettingsStore(string path)
{
    public AppSettings Load()
    {
        if (!File.Exists(path)) return AppSettings.Default;
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
        if (settings is null || string.IsNullOrWhiteSpace(settings.CaptureFolder) || !Path.IsPathFullyQualified(settings.CaptureFolder))
            throw new InvalidDataException("Invalid capture folder in settings.");
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}

public readonly record struct PixelRect(int X, int Y, int Width, int Height);
public static class SelectionGeometry
{
    // Inputs and result use physical desktop pixels, never WPF device-independent units.
    public static PixelRect Between(int x1, int y1, int x2, int y2, PixelRect desktop)
    {
        int left = Math.Clamp(Math.Min(x1, x2), desktop.X, desktop.X + desktop.Width);
        int top = Math.Clamp(Math.Min(y1, y2), desktop.Y, desktop.Y + desktop.Height);
        int right = Math.Clamp(Math.Max(x1, x2), desktop.X, desktop.X + desktop.Width);
        int bottom = Math.Clamp(Math.Max(y1, y2), desktop.Y, desktop.Y + desktop.Height);
        return new(left, top, right - left, bottom - top);
    }
}

public static class CaptureFiles
{
    public static string NewPath(string folder) => Path.Combine(folder, $"Capture-{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.png");
    public static IEnumerable<string> List(string folder) => Directory.Exists(folder)
        ? Directory.EnumerateFiles(folder, "*.png").OrderByDescending(File.GetLastWriteTimeUtc)
        : [];
}

public record CaptureResult(string? SavedPath, string? SaveError, string? ClipboardError);
public static class CaptureCoordinator
{
    public static async Task<CaptureResult> Complete(Func<Task<string>> save, Func<Task> copy)
    {
        string? path = null, saveError = null, clipboardError = null;
        try { path = await save(); } catch (Exception ex) { saveError = ex.Message; }
        try { await copy(); } catch (Exception ex) { clipboardError = ex.Message; }
        return new(path, saveError, clipboardError);
    }
}
