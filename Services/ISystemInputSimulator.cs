namespace AISpeech.Services;

/// <summary>
/// Abstracts OS-level window focus and keyboard input injection,
/// allowing tests to verify auto-paste logic without real P/Invoke calls.
/// </summary>
public interface ISystemInputSimulator
{
    void ForceForegroundWindow(IntPtr hWnd);
    void SimulateCtrlV();
}
