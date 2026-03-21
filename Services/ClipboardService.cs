using System.Runtime.InteropServices;

namespace AISpeech.Services;

public sealed class ClipboardService
{
    private readonly Control _marshalControl;
    private readonly ISystemInputSimulator _inputSimulator;

    public ClipboardService(Control marshalControl, ISystemInputSimulator? inputSimulator = null)
    {
        _marshalControl = marshalControl;
        _inputSimulator = inputSimulator ?? new SystemInputSimulator();
    }

    /// <summary>
    /// Captures the current foreground window handle. Call this while the user
    /// is still focused on their target app (e.g. when recording stops), before
    /// any marshaling that might shift focus.
    /// </summary>
    public static IntPtr CaptureTargetWindow() => GetForegroundWindow();

    public Task SetTextAsync(string text, bool autoPaste = false, IntPtr targetWindow = default)
    {
        var tcs = new TaskCompletionSource();
        _marshalControl.BeginInvoke(async () =>
        {
            try
            {
                Clipboard.SetText(text);

                if (autoPaste && targetWindow != IntPtr.Zero)
                {
                    await Task.Delay(100);
                    _inputSimulator.ForceForegroundWindow(targetWindow);
                    await Task.Delay(50);
                    _inputSimulator.SimulateCtrlV();
                }

                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    #region P/Invoke

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    #endregion
}
