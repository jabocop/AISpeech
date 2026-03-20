using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AISpeech;

public sealed class TrayIconManager : IDisposable
{
    private readonly Icon _idleIcon;
    private readonly Icon _recordingIcon;
    private readonly Icon _processingIcon;
    private bool _disposed;

    private static readonly Color IdleColor = Color.FromArgb(136, 136, 136);
    private static readonly Color RecordingColor = Color.FromArgb(255, 68, 68);
    private static readonly Color ProcessingColor = Color.FromArgb(255, 170, 0);

    public TrayIconManager()
    {
        _idleIcon = GenerateIcon(IdleColor);
        _recordingIcon = GenerateIcon(RecordingColor);
        _processingIcon = GenerateIcon(ProcessingColor);
    }

    public Icon GetIcon(RecordingState state) => state switch
    {
        RecordingState.Recording => _recordingIcon,
        RecordingState.Processing => _processingIcon,
        _ => _idleIcon
    };

    private static Icon GenerateIcon(Color fillColor)
    {
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(fillColor);
            using var pen = new Pen(Color.FromArgb(60, 60, 60), 1f);
            g.FillEllipse(brush, 1, 1, 13, 13);
            g.DrawEllipse(pen, 1, 1, 13, 13);
        }

        var hIcon = bitmap.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        // Clone so we own the icon and can free the native handle
        var cloned = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        bitmap.Dispose();
        return cloned;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _idleIcon.Dispose();
        _recordingIcon.Dispose();
        _processingIcon.Dispose();
    }
}
