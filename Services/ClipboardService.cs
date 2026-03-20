using System.Runtime.InteropServices;

namespace AISpeech.Services;

public sealed class ClipboardService
{
    private readonly Control _marshalControl;

    public ClipboardService(Control marshalControl)
    {
        _marshalControl = marshalControl;
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
                    ForceForegroundWindow(targetWindow);
                    await Task.Delay(50);
                    SimulateCtrlV();
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

    /// <summary>
    /// Forces the target window to the foreground by temporarily attaching to
    /// its input thread. Plain SetForegroundWindow silently fails unless the
    /// caller is already the foreground process.
    /// </summary>
    private static void ForceForegroundWindow(IntPtr hWnd)
    {
        var targetThread = GetWindowThreadProcessId(hWnd, out _);
        var currentThread = GetCurrentThreadId();

        if (targetThread != currentThread)
        {
            AttachThreadInput(currentThread, targetThread, true);
            SetForegroundWindow(hWnd);
            AttachThreadInput(currentThread, targetThread, false);
        }
        else
        {
            SetForegroundWindow(hWnd);
        }
    }

    private static void SimulateCtrlV()
    {
        var inputs = new INPUT[]
        {
            CreateKeyInput(VK_CONTROL, keyUp: false),
            CreateKeyInput(VK_V, keyUp: false),
            CreateKeyInput(VK_V, keyUp: true),
            CreateKeyInput(VK_CONTROL, keyUp: true),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT CreateKeyInput(ushort keyCode, bool keyUp) => new()
    {
        type = INPUT_KEYBOARD,
        ki = new KEYBDINPUT
        {
            wVk = keyCode,
            dwFlags = keyUp ? KEYEVENTF_KEYUP : 0u,
        }
    };

    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    #region P/Invoke

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    // Native INPUT is 40 bytes on x64: 4 (type) + 4 (padding) + 32 (union sized to MOUSEINPUT).
    // Must match exactly or SendInput returns ERROR_INVALID_PARAMETER (87).
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    #endregion
}
