using System.Runtime.InteropServices;

namespace AISpeech.Services;

/// <summary>
/// Production implementation that uses Win32 P/Invoke for window focus
/// and keyboard input injection.
/// </summary>
public sealed class SystemInputSimulator : ISystemInputSimulator
{
    public void ForceForegroundWindow(IntPtr hWnd)
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

    public void SimulateCtrlV()
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
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

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
