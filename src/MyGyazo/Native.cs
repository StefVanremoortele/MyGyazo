using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace MyGyazo;

internal static class Native
{
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr handle, int id);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X; public int Y; }
}

internal sealed class Hotkey : IDisposable
{
    private readonly HwndSource source = new(new HwndSourceParameters("My Gyazo hotkey") { ParentWindow = new IntPtr(-3) });
    private int activeId;
    private int nextId = 1;
    private uint modifiers, key;
    public event Action? Pressed;
    public Hotkey() => source.AddHook(Handle);
    public bool Register(uint newModifiers, uint newKey)
    {
        if (activeId != 0 && modifiers == newModifiers && key == newKey) return true;
        var id = nextId++;
        if (!Native.RegisterHotKey(source.Handle, id, newModifiers | 0x4000, newKey)) return false;
        if (activeId != 0) Native.UnregisterHotKey(source.Handle, activeId);
        activeId = id; modifiers = newModifiers; key = newKey;
        return true;
    }
    private IntPtr Handle(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312 && wParam.ToInt32() == activeId) { handled = true; Pressed?.Invoke(); }
        return IntPtr.Zero;
    }
    public void Dispose() { if (activeId != 0) Native.UnregisterHotKey(source.Handle, activeId); source.Dispose(); }
}
