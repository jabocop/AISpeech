using SharpHook;
using SharpHook.Native;

namespace AISpeech.Services;

public sealed class HotkeyManagerService : IDisposable
{
    private readonly TaskPoolGlobalHook _hook;
    private readonly HotkeyBinding _binding;
    private readonly HashSet<KeyCode> _heldModifiers = new();
    private bool _active;
    private bool _disposed;

    public event Action? RecordingStarted;
    public event Action? RecordingStopped;

    public HotkeyManagerService(AppSettings settings)
    {
        _binding = ParseBinding(settings.HotkeyModifiers, settings.HotkeyKey);

        _hook = new TaskPoolGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    public async Task StartAsync()
    {
        await Task.Run(() => _hook.RunAsync());
    }

    public void Stop()
    {
        _hook.Dispose();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var key = e.Data.KeyCode;

        if (IsModifierKey(key))
            _heldModifiers.Add(NormalizeModifier(key));

        if (_active)
            return;

        if (_binding.Matches(key, _heldModifiers))
        {
            _active = true;
            RecordingStarted?.Invoke();
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        var key = e.Data.KeyCode;

        if (IsModifierKey(key))
            _heldModifiers.Remove(NormalizeModifier(key));

        if (!_active)
            return;

        if (_binding.ShouldDeactivate(key))
        {
            _active = false;
            RecordingStopped?.Invoke();
        }
    }

    private static HotkeyBinding ParseBinding(string modifiers, string key)
    {
        var mods = ParseModifiers(modifiers);
        var modifierOnly = string.IsNullOrWhiteSpace(key);
        KeyCode? triggerKey = modifierOnly ? null : ParseKey(key);

        if (modifierOnly && mods.Count < 2)
            throw new ArgumentException("Modifier-only hotkey requires at least 2 modifiers.");

        return new HotkeyBinding(mods, triggerKey, modifierOnly);
    }

    private static HashSet<KeyCode> ParseModifiers(string modifiers)
    {
        var result = new HashSet<KeyCode>();
        foreach (var part in modifiers.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            result.Add(part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => KeyCode.VcLeftControl,
                "shift" => KeyCode.VcLeftShift,
                "alt" => KeyCode.VcLeftAlt,
                "win" or "meta" => KeyCode.VcLeftMeta,
                _ => throw new ArgumentException($"Unknown modifier: {part}")
            });
        }
        return result;
    }

    private static KeyCode ParseKey(string key)
    {
        if (Enum.TryParse<KeyCode>($"Vc{key}", ignoreCase: true, out var keyCode))
            return keyCode;

        throw new ArgumentException($"Unknown key: {key}");
    }

    private static KeyCode NormalizeModifier(KeyCode key) => key switch
    {
        KeyCode.VcRightControl => KeyCode.VcLeftControl,
        KeyCode.VcRightShift => KeyCode.VcLeftShift,
        KeyCode.VcRightAlt => KeyCode.VcLeftAlt,
        KeyCode.VcRightMeta => KeyCode.VcLeftMeta,
        _ => key
    };

    private static bool IsModifierKey(KeyCode key) =>
        key is KeyCode.VcLeftControl or KeyCode.VcRightControl
            or KeyCode.VcLeftShift or KeyCode.VcRightShift
            or KeyCode.VcLeftAlt or KeyCode.VcRightAlt
            or KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hook.Dispose();
    }

    private sealed class HotkeyBinding(
        HashSet<KeyCode> requiredModifiers,
        KeyCode? triggerKey,
        bool modifierOnly)
    {
        public bool Matches(KeyCode pressedKey, HashSet<KeyCode> heldModifiers)
        {
            if (!AllModifiersHeld(heldModifiers))
                return false;

            return modifierOnly
                ? IsModifierKey(pressedKey)
                : pressedKey == triggerKey;
        }

        public bool ShouldDeactivate(KeyCode releasedKey)
        {
            if (modifierOnly)
                return requiredModifiers.Contains(NormalizeModifier(releasedKey));

            return releasedKey == triggerKey || requiredModifiers.Contains(NormalizeModifier(releasedKey));
        }

        private bool AllModifiersHeld(HashSet<KeyCode> heldModifiers)
        {
            foreach (var mod in requiredModifiers)
            {
                if (!heldModifiers.Contains(mod))
                    return false;
            }
            return true;
        }
    }
}
