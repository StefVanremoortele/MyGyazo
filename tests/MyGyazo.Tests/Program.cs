using MyGyazo.Core;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); passed++;
}

var desktop = new PixelRect(-1920, -200, 5760, 2360);
Check(SelectionGeometry.Between(200, 400, 50, 100, desktop) == new PixelRect(50, 100, 150, 300), "Reverse drag is normalized");
Check(SelectionGeometry.Between(-1800, -100, -1200, 500, desktop) == new PixelRect(-1800, -100, 600, 600), "Negative monitor coordinates are preserved");
Check(SelectionGeometry.Between(-5000, -3000, 5000, 3000, desktop) == desktop, "Selection clips to desktop bounds");
Check(SelectionGeometry.Between(100, 100, 100, 100, desktop).Width == 0, "Click without dragging is empty");
// A 100% monitor to the left and a 150% monitor to the right: cursor input is
// already physical pixels, so crossing the boundary must never apply a DPI ratio.
Check(SelectionGeometry.Between(-300, 100, 450, 550, desktop) == new PixelRect(-300, 100, 750, 450), "Mixed-scale boundary keeps physical dimensions");
Check(SelectionGeometry.Between(150, 150, 600, 600, desktop).Width == 450, "150% monitor selection is not rescaled");

var root = Path.Combine(Path.GetTempPath(), "MyGyazo.Tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var store = new SettingsStore(Path.Combine(root, "settings", "settings.json"));
    Check(store.Load() == AppSettings.Default, "Missing settings use defaults");
    var settings = new AppSettings(root, 3, 0x41, true);
    store.Save(settings);
    Check(store.Load() == settings, "Settings round-trip folder, hotkey and startup preference");
    store.Save(settings with { LaunchAtLogin = false });
    Check(!store.Load().LaunchAtLogin, "Settings can be replaced atomically");
    File.WriteAllText(Path.Combine(root, "settings", "settings.json"), "{");
    bool corruptRejected = false;
    try { store.Load(); } catch (System.Text.Json.JsonException) { corruptRejected = true; }
    Check(corruptRejected, "Corrupt settings are reported to the caller");
    Check(Enumerable.Range(0, 1000).Select(_ => CaptureFiles.NewPath(root)).Distinct().Count() == 1000, "Rapid capture filenames are unique");
    Check(!CaptureFiles.List(Path.Combine(root, "absent")).Any(), "Missing capture folder gives empty history");
    var older = Path.Combine(root, "older.png"); var newer = Path.Combine(root, "newer.png");
    File.WriteAllText(older, "test"); File.WriteAllText(newer, "test");
    File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddMinutes(-10));
    File.WriteAllText(Path.Combine(root, "incomplete.png.tmp"), "test");
    Check(CaptureFiles.List(root).SequenceEqual(new[] { newer, older }), "History is newest first and excludes incomplete files");
    File.Delete(newer);
    Check(CaptureFiles.List(root).Single() == older, "History reflects externally deleted files");

    bool copied = false;
    var result = await CaptureCoordinator.Complete(() => Task.FromResult("capture.png"), () => { copied = true; return Task.CompletedTask; });
    Check(result == new CaptureResult("capture.png", null, null) && copied, "Successful capture saves and copies");
    copied = false;
    result = await CaptureCoordinator.Complete(() => throw new IOException("disk full"), () => { copied = true; return Task.CompletedTask; });
    Check(result.SavedPath is null && result.SaveError == "disk full" && copied, "Save failure still copies image");
    result = await CaptureCoordinator.Complete(() => Task.FromResult("capture.png"), () => throw new IOException("clipboard busy"));
    Check(result.SavedPath == "capture.png" && result.ClipboardError == "clipboard busy", "Clipboard failure preserves saved PNG");
    result = await CaptureCoordinator.Complete(() => throw new IOException("disk full"), () => throw new IOException("clipboard busy"));
    Check(result.SaveError is not null && result.ClipboardError is not null, "Both failures are reported independently");
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"All {passed} checks passed.");
