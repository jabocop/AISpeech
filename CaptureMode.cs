namespace AISpeech;

public enum CaptureMode
{
    /// <summary>Speech trigger "use plan" — Prompt refinement mode for Claude prompts.</summary>
    Prompt,

    /// <summary>No trigger phrase — Clean transcription only.</summary>
    Transcribe,

    /// <summary>Speech trigger "use ralph" — Structured task-plan mode.</summary>
    Ralph
}
