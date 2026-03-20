# AISpeech

A Windows system tray application that captures speech via a global hotkey, transcribes it locally using [Whisper.net](https://github.com/sandrohanea/whisper.net), and optionally refines the transcription using Azure OpenAI. The result is copied to your clipboard.

## How it works

1. Hold a global hotkey (default: **Ctrl+Win**) and speak
2. Release the hotkey — audio is transcribed locally via Whisper
3. A **speech trigger phrase** at any point in your speech selects the processing mode
4. If Azure OpenAI is configured, the transcription is refined based on the detected mode
5. The final text is copied to your clipboard

## Speech trigger modes

The mode is determined by what you say, not which hotkey you press. Trigger phrases are detected and stripped from the text before processing.

| Trigger phrase | Mode | Description |
|---|---|---|
| *(none)* | **Transcribe** | Cleans up filler words, grammar, and speech-to-text errors |
| "use plan" | **Prompt** | Refines speech into a structured planning prompt for Claude |
| "use ralph" | **Ralph** | Produces a prompt instructing Claude to create a structured task plan with `[Task ID] - [Status]` format in a `plan-<ticket>.md` file |

Triggers are configurable in `appsettings.json` under `SpeechTriggers`.

### Reprocessing with a different mode

If the wrong mode was detected (e.g. you said "use plan" but Whisper didn't pick it up, or you want to try a different mode), you can reprocess the last transcription without re-recording:

1. Right-click the tray icon
2. Select **Reprocess as...** → pick the desired mode (Transcribe, Prompt, or Ralph)

This sends the last raw transcription through Azure OpenAI with the selected mode and copies the new result to your clipboard. The option is disabled until the first transcription completes.

## Prerequisites

- Windows 10+ (targets `net10.0-windows10.0.17763.0`)
- .NET 10 SDK
- A microphone
- *(Optional)* Azure OpenAI deployment for text refinement

## Setup

```bash
# Clone and build
dotnet build

# Copy the local config template and fill in your Azure OpenAI credentials
copy appsettings.local.template.json appsettings.local.json
```

Edit `appsettings.local.json` with your Azure OpenAI details:

```json
{
  "AISpeech": {
    "AzureOpenAIEndpoint": "https://<your-resource>.openai.azure.com/",
    "AzureOpenAIApiKey": "<your-api-key>",
    "AzureOpenAIDeployment": "<your-deployment-name>",
    "AzureOpenAIApiVersion": "2025-04-01-preview"
  }
}
```

Without Azure OpenAI configured, the app still works — it just returns the raw Whisper transcription.

## Running

```bash
dotnet run
```

The app runs in the system tray. Right-click the tray icon for options.

### Tray icon states

- **Gray** — Idle, ready to record
- **Red** — Recording
- **Orange** — Processing (transcribing / refining)

## Configuration

All settings live in `appsettings.json` (defaults) and `appsettings.local.json` (secrets, git-ignored).

| Setting | Default | Description |
|---|---|---|
| `HotkeyModifiers` | `Ctrl+Win` | Modifier keys for the hotkey (`Ctrl`, `Shift`, `Alt`, `Win`) |
| `HotkeyKey` | *(empty)* | Optional key to combine with modifiers. Empty = modifier-only hotkey |
| `SpeechTriggers` | see below | List of `{ Phrase, Mode }` trigger definitions |
| `WhisperModelPath` | `models/ggml-base.bin` | Path to the Whisper GGML model file |
| `WhisperModelType` | `Base` | Model size: `Tiny`, `Base`, `Small`, `Medium`, `Large` |
| `Language` | `en` | Transcription language |

The Whisper model is downloaded automatically on first run if not present.

### Adding custom speech triggers

Add entries to the `SpeechTriggers` array in `appsettings.json`:

```json
"SpeechTriggers": [
  { "Phrase": "use plan", "Mode": "Prompt" },
  { "Phrase": "use ralph", "Mode": "Ralph" }
]
```

Phrases are matched case-insensitively anywhere in the transcription. The first match wins.

## Project structure

```
AISpeech/
├── Program.cs                    Entry point, config loading
├── AppSettings.cs                Configuration classes
├── CaptureMode.cs                Enum: Prompt, Transcribe, Ralph
├── RecordingState.cs             Enum: Idle, Recording, Processing
├── TrayApplicationContext.cs     Main app lifecycle, coordinates services
├── TrayIconManager.cs            Dynamic tray icon generation
├── DebugForm.cs                  Debug log window
├── appsettings.json              Default configuration
├── appsettings.local.json        Local secrets (git-ignored)
└── Services/
    ├── AudioRecorderService.cs   Microphone capture (NAudio, 16kHz mono)
    ├── TranscriptionService.cs   Local Whisper transcription
    ├── HotkeyManagerService.cs   Global hotkey detection (SharpHook)
    ├── PhraseTriggerService.cs   Speech trigger phrase detection
    ├── TextCleanupService.cs     Azure OpenAI text refinement
    ├── ClipboardService.cs       Thread-safe clipboard access
    └── NotificationService.cs    Windows toast notifications
```

## Dependencies

- [NAudio](https://github.com/naudio/NAudio) — Audio capture
- [Whisper.net](https://github.com/sandrohanea/whisper.net) — Local speech-to-text
- [SharpHook](https://github.com/TolikPyl662/SharpHook) — Global keyboard hooks
- [Microsoft.Toolkit.Uwp.Notifications](https://github.com/CommunityToolkit/WindowsCommunityToolkit) — Toast notifications
