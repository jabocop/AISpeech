namespace AISpeech.Services;

public sealed class PhraseTriggerService
{
    private readonly List<(string Phrase, CaptureMode Mode)> _triggers;

    public PhraseTriggerService(AppSettings settings)
    {
        _triggers = new List<(string, CaptureMode)>();

        foreach (var trigger in settings.SpeechTriggers)
        {
            if (string.IsNullOrWhiteSpace(trigger.Phrase))
                continue;

            if (!Enum.TryParse<CaptureMode>(trigger.Mode, ignoreCase: true, out var mode))
                throw new ArgumentException($"Unknown capture mode: {trigger.Mode}");

            _triggers.Add((trigger.Phrase, mode));
        }
    }

    public IReadOnlyList<(string Phrase, CaptureMode Mode)> Triggers => _triggers;

    public (CaptureMode Mode, string CleanedText) Detect(string rawText)
    {
        foreach (var (phrase, mode) in _triggers)
        {
            var index = rawText.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var cleaned = string.Concat(rawText.AsSpan(0, index), rawText.AsSpan(index + phrase.Length)).Trim();
                return (mode, cleaned);
            }
        }

        return (CaptureMode.Transcribe, rawText);
    }
}
