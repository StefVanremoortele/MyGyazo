using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MyGyazo;

// Native integration is shared separately from the WinUI XAML application.

internal static class Native
{
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr handle, int id);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X; public int Y; }
}

internal sealed class Hotkey : NativeWindow, IDisposable
{
    private int activeId;
    private int nextId = 1;
    private uint modifiers, key;
    internal event Action? Pressed;
    internal Hotkey() => CreateHandle(new CreateParams { Caption = "My Gyazo hotkey", Parent = new IntPtr(-3) });
    internal bool Register(uint newModifiers, uint newKey)
    {
        if (activeId != 0 && modifiers == newModifiers && key == newKey) return true;
        int id = nextId++;
        if (!Native.RegisterHotKey(Handle, id, newModifiers | 0x4000, newKey)) return false;
        if (activeId != 0) Native.UnregisterHotKey(Handle, activeId);
        activeId = id; modifiers = newModifiers; key = newKey;
        return true;
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x0312 && message.WParam.ToInt32() == activeId) Pressed?.Invoke();
        base.WndProc(ref message);
    }
    public void Dispose() { if (activeId != 0) Native.UnregisterHotKey(Handle, activeId); DestroyHandle(); }
}

internal sealed class WindowSizing : NativeWindow, IDisposable
{
    internal WindowSizing(IntPtr handle) => AssignHandle(handle);
    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo { public Native.Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);
        if (message.Msg != 0x0024) return;
        var info = Marshal.PtrToStructure<MinMaxInfo>(message.LParam);
        double scale = Native.GetDpiForWindow(Handle) / 96d;
        info.MinTrackSize = new Native.Point { X = (int)(860 * scale), Y = (int)(600 * scale) };
        Marshal.StructureToPtr(info, message.LParam, false);
    }
    public void Dispose() => ReleaseHandle();
}
