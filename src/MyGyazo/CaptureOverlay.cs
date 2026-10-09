using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using MyGyazo.Core;

namespace MyGyazo;

// A pixel-based native overlay avoids mixing WPF DIPs across differently scaled monitors.
internal sealed class CaptureOverlay : Form
{
    private readonly Bitmap desktop;
    private readonly Rectangle bounds;
    private Native.Point start;
    private PixelRect selection;
    private bool dragging;
    public Bitmap? Result { get; private set; }

    public CaptureOverlay()
    {
        bounds = SystemInformation.VirtualScreen;
        desktop = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        try { using var graphics = Graphics.FromImage(desktop); graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy); }
        catch { desktop.Dispose(); throw; }
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true; ShowInTaskbar = false; DoubleBuffered = true; KeyPreview = true;
        Cursor = Cursors.Cross;
        AccessibleName = "Select screenshot area. Drag to capture; Escape to cancel.";
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Native.SetWindowPos(Handle, new IntPtr(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x0040);
        Activate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(desktop, 0, 0);
        using var shade = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
        e.Graphics.FillRectangle(shade, ClientRectangle);
        if (selection.Width <= 0 || selection.Height <= 0) return;
        var rect = new Rectangle(selection.X - bounds.X, selection.Y - bounds.Y, selection.Width, selection.Height);
        e.Graphics.DrawImage(desktop, rect, rect, GraphicsUnit.Pixel);
        using var pen = new Pen(Color.DeepSkyBlue, 2);
        e.Graphics.DrawRectangle(pen, rect);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { Close(); return; }
        if (e.Button != MouseButtons.Left) return;
        Native.GetCursorPos(out start); dragging = true; Capture = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!dragging) return;
        UpdateSelection(); Invalidate();
    }
    private void UpdateSelection()
    {
        Native.GetCursorPos(out var end);
        selection = SelectionGeometry.Between(start.X, start.Y, end.X, end.Y, new(bounds.X, bounds.Y, bounds.Width, bounds.Height));
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!dragging || e.Button != MouseButtons.Left) return;
        UpdateSelection(); dragging = false; Capture = false;
        if (selection.Width > 0 && selection.Height > 0)
            Result = desktop.Clone(new Rectangle(selection.X - bounds.X, selection.Y - bounds.Y, selection.Width, selection.Height), PixelFormat.Format32bppArgb);
        Close();
    }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); base.OnKeyDown(e); }
    protected override void Dispose(bool disposing) { if (disposing) desktop.Dispose(); base.Dispose(disposing); }
}
