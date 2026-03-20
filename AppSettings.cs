namespace AISpeech;

public sealed class SpeechTriggerConfig
{
    public string Phrase { get; set; } = "";
    public string Mode { get; set; } = "Prompt";
}

public sealed class AppSettings
{
    public string HotkeyModifiers { get; set; } = "Ctrl+Win";
    public string HotkeyKey { get; set; } = "";

    public List<SpeechTriggerConfig> SpeechTriggers { get; set; } = new()
    {
        new SpeechTriggerConfig { Phrase = "use plan", Mode = "Prompt" },
        new SpeechTriggerConfig { Phrase = "use ralph", Mode = "Ralph" }
    };

    public string WhisperModelPath { get; set; } = "models/ggml-base.bin";
    public string WhisperModelType { get; set; } = "Base";
    public string Language { get; set; } = "en";

    // Azure OpenAI — configure in appsettings.local.json (gitignored)
    public string AzureOpenAIEndpoint { get; set; } = "";
    public string AzureOpenAIApiKey { get; set; } = "";
    public string AzureOpenAIDeployment { get; set; } = "";
    public string AzureOpenAIApiVersion { get; set; } = "2024-02-15-preview";
}
