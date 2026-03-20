using System.Diagnostics;
using AISpeech.Services;

namespace AISpeech;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AppSettings _settings;
    private readonly DebugForm _debugForm;
    private readonly NotifyIcon _trayIcon;
    private readonly AudioRecorderService _recorder;
    private readonly TranscriptionService _transcriber;
    private readonly HotkeyManagerService _hotkeyManager;
    private readonly ClipboardService _clipboard;
    private readonly TextCleanupService _textCleanup;
    private readonly PhraseTriggerService _phraseTrigger;
    private readonly TrayIconManager _iconManager;
    private readonly Form _marshalForm;
    private readonly Stopwatch _recordingStopwatch = new();

    private int _state = (int)RecordingState.Idle;
    private string? _lastTranscription;
    private string? _lastRawText;
    private ToolStripMenuItem? _reprocessMenu;

    private static readonly TimeSpan MinRecordingDuration = TimeSpan.FromMilliseconds(500);

    public TrayApplicationContext(AppSettings settings, DebugForm debugForm)
    {
        _settings = settings;
        _debugForm = debugForm;

        // Hidden form on the STA/UI thread — forces HWND creation for marshaling
        _marshalForm = new Form { ShowInTaskbar = false, WindowState = FormWindowState.Minimized };
        _marshalForm.Show();
        _marshalForm.Hide();

        _iconManager = new TrayIconManager();

        _trayIcon = new NotifyIcon
        {
            Icon = _iconManager.GetIcon(RecordingState.Idle),
            Text = "AISpeech — Idle",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _recorder = new AudioRecorderService();
        _transcriber = new TranscriptionService(settings);
        _textCleanup = new TextCleanupService(settings);
        _phraseTrigger = new PhraseTriggerService(settings);
        _clipboard = new ClipboardService(_marshalForm);
        _hotkeyManager = new HotkeyManagerService(settings);

        _hotkeyManager.RecordingStarted += OnRecordingStarted;
        _hotkeyManager.RecordingStopped += OnRecordingStopped;

        Log($"Hotkey: {settings.HotkeyModifiers}+{settings.HotkeyKey}".TrimEnd('+'));
        foreach (var trigger in _phraseTrigger.Triggers)
            Log($"Speech trigger: \"{trigger.Phrase}\" → {trigger.Mode}");
        Log($"Whisper model: {settings.WhisperModelPath} ({settings.WhisperModelType}), runtime: {settings.WhisperRuntime}, language: {settings.Language}");
        Log($"Azure OpenAI cleanup: {(_textCleanup.IsConfigured ? "enabled" : "disabled (no config)")}");

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            Log("Initializing Whisper model...");
            await _transcriber.InitializeAsync();
            Log("Whisper model ready.");

            Log("Starting global hotkey hook...");
            await _hotkeyManager.StartAsync();
            Log("Hotkey hook active.");

            SetTrayState(RecordingState.Idle, "Ready");
            Log("Initialization complete — ready.");
        }
        catch (Exception ex)
        {
            LogError($"Startup failed: {ex.Message}");
            NotificationService.ShowError($"Startup failed: {ex.Message}");
            SetTrayState(RecordingState.Idle, "Error");
        }
    }

    private void OnRecordingStarted()
    {
        if (Interlocked.CompareExchange(ref _state, (int)RecordingState.Recording, (int)RecordingState.Idle)
            != (int)RecordingState.Idle)
        {
            Log("Recording start ignored — not idle.", DebugForm.LogLevel.Warn);
            return;
        }

        try
        {
            _recorder.StartRecording();
            _recordingStopwatch.Restart();
            SetTrayState(RecordingState.Recording);
            Log("Recording started.");
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _state, (int)RecordingState.Idle);
            SetTrayState(RecordingState.Idle);
            LogError($"Microphone error: {ex.Message}");
            NotificationService.ShowError($"Microphone error: {ex.Message}");
        }
    }

    private void OnRecordingStopped()
    {
        if (Interlocked.CompareExchange(ref _state, (int)RecordingState.Processing, (int)RecordingState.Recording)
            != (int)RecordingState.Recording)
        {
            Log("Recording stop ignored — not recording.", DebugForm.LogLevel.Warn);
            return;
        }

        _recordingStopwatch.Stop();
        Log($"Recording stopped. Duration: {_recordingStopwatch.Elapsed.TotalSeconds:F2}s");
        _ = ProcessRecordingAsync();
    }

    private async Task ProcessRecordingAsync()
    {
        try
        {
            using var wavStream = _recorder.StopRecording();
            Log($"WAV stream size: {wavStream.Length} bytes");

            if (_recordingStopwatch.Elapsed < MinRecordingDuration)
            {
                Log("Recording too short — skipped.", DebugForm.LogLevel.Warn);
                NotificationService.ShowInfo("Recording too short — skipped.");
                return;
            }

            SetTrayState(RecordingState.Processing);
            Log("Transcribing...");

            var text = await _transcriber.TranscribeAsync(wavStream);

            if (string.IsNullOrWhiteSpace(text))
            {
                Log("No speech detected.", DebugForm.LogLevel.Warn);
                NotificationService.ShowInfo("No speech detected.");
                return;
            }

            Log($"Transcription (raw): \"{text}\"");

            var (mode, cleanedText) = _phraseTrigger.Detect(text);
            Log($"Phrase detection: mode={mode}");
            text = cleanedText;
            _lastRawText = text;

            if (_textCleanup.IsConfigured)
            {
                Log($"Processing via Azure OpenAI (mode: {mode})...");
                text = await _textCleanup.CleanupAsync(text, mode);
                Log($"Result: \"{text}\"");
            }

            _lastTranscription = text;
            UpdateReprocessMenu();
            await _clipboard.SetTextAsync(text);
            Log("Text copied to clipboard.");
        }
        catch (Exception ex)
        {
            LogError($"Transcription failed: {ex.Message}");
            NotificationService.ShowError($"Transcription failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _state, (int)RecordingState.Idle);
            SetTrayState(RecordingState.Idle);
            Log("State → Idle");
        }
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        _reprocessMenu = new ToolStripMenuItem("Reprocess as...") { Enabled = false };
        foreach (var mode in Enum.GetValues<CaptureMode>())
            _reprocessMenu.DropDownItems.Add(mode.ToString(), null, (_, _) => _ = ReprocessAsync(mode));
        menu.Items.Add(_reprocessMenu);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Show Debug Log", null, (_, _) => _debugForm.Show());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private void UpdateReprocessMenu()
    {
        if (_reprocessMenu is not null)
            _marshalForm.BeginInvoke(() => _reprocessMenu.Enabled = _lastRawText is not null);
    }

    private async Task ReprocessAsync(CaptureMode mode)
    {
        if (_lastRawText is null)
            return;

        if (Interlocked.CompareExchange(ref _state, (int)RecordingState.Processing, (int)RecordingState.Idle)
            != (int)RecordingState.Idle)
        {
            Log("Reprocess ignored — not idle.", DebugForm.LogLevel.Warn);
            return;
        }

        try
        {
            SetTrayState(RecordingState.Processing);
            var text = _lastRawText;
            Log($"Reprocessing as {mode}: \"{text}\"");

            if (_textCleanup.IsConfigured)
            {
                text = await _textCleanup.CleanupAsync(text, mode);
                Log($"Result: \"{text}\"");
            }

            _lastTranscription = text;
            await _clipboard.SetTextAsync(text);
            Log("Text copied to clipboard.");
        }
        catch (Exception ex)
        {
            LogError($"Reprocess failed: {ex.Message}");
            NotificationService.ShowError($"Reprocess failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _state, (int)RecordingState.Idle);
            SetTrayState(RecordingState.Idle);
            Log("State → Idle");
        }
    }

    private void ExitApplication()
    {
        Log("Shutting down...");
        _hotkeyManager.Stop();
        _trayIcon.Visible = false;
        _recorder.Dispose();
        _transcriber.Dispose();
        _textCleanup.Dispose();
        _hotkeyManager.Dispose();
        _debugForm.Close();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
            _iconManager.Dispose();
            _marshalForm.Dispose();
            _recorder.Dispose();
            _transcriber.Dispose();
            _textCleanup.Dispose();
            _hotkeyManager.Dispose();
            _debugForm.Dispose();
        }
        base.Dispose(disposing);
    }

    private void SetTrayState(RecordingState state, string? statusOverride = null)
    {
        var status = statusOverride ?? state switch
        {
            RecordingState.Recording => "Recording...",
            RecordingState.Processing => "Transcribing...",
            _ => "Idle"
        };
        _trayIcon.Icon = _iconManager.GetIcon(state);

        var tooltip = $"AISpeech — {status}";
        if (state == RecordingState.Idle && _lastTranscription is not null)
        {
            // NotifyIcon.Text max is 127 chars
            var maxTextLen = 127 - tooltip.Length - 1; // 1 for newline
            var preview = _lastTranscription.Length > maxTextLen
                ? _lastTranscription[..maxTextLen]
                : _lastTranscription;
            tooltip = $"{tooltip}\n{preview}";
        }
        _trayIcon.Text = tooltip;
    }

    private void Log(string message, DebugForm.LogLevel level = DebugForm.LogLevel.Info)
        => _debugForm.Log(message, level);

    private void LogError(string message)
        => _debugForm.Log(message, DebugForm.LogLevel.Error);
}
