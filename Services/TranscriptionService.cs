using Whisper.net;
using Whisper.net.Ggml;
using Whisper.net.LibraryLoader;

namespace AISpeech.Services;

public sealed class TranscriptionService : IDisposable
{
    private WhisperFactory? _factory;
    private readonly string _modelPath;
    private readonly GgmlType _modelType;
    private readonly string _language;
    private readonly string _runtime;
    private bool _disposed;

    public TranscriptionService(AppSettings settings)
    {
        _modelPath = settings.WhisperModelPath;
        _modelType = Enum.Parse<GgmlType>(settings.WhisperModelType, ignoreCase: true);
        _language = settings.Language;
        _runtime = settings.WhisperRuntime;

        ConfigureRuntime(_runtime);
    }

    private static void ConfigureRuntime(string runtime)
    {
        RuntimeOptions.RuntimeLibraryOrder = runtime.ToLowerInvariant() switch
        {
            "cuda" => [RuntimeLibrary.Cuda, RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu],
            "vulkan" => [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu],
            "cpu" => [RuntimeLibrary.Cpu],
            _ => [RuntimeLibrary.Cuda, RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu],
        };
    }

    public async Task InitializeAsync()
    {
        var fullPath = Path.IsPathRooted(_modelPath)
            ? _modelPath
            : Path.Combine(AppContext.BaseDirectory, _modelPath);

        if (!File.Exists(fullPath))
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(_modelType);
            using var fileStream = File.Create(fullPath);
            await modelStream.CopyToAsync(fileStream);
        }

        var useGpu = !_runtime.Equals("Cpu", StringComparison.OrdinalIgnoreCase);
        _factory = WhisperFactory.FromPath(fullPath, new WhisperFactoryOptions { UseGpu = useGpu });

        LoadedRuntime = RuntimeOptions.LoadedLibrary.ToString();
    }

    public string? LoadedRuntime { get; private set; }

    public async Task<string> TranscribeAsync(MemoryStream wavStream)
    {
        if (_factory is null)
            throw new InvalidOperationException("TranscriptionService not initialized. Call InitializeAsync first.");

        await using var processor = _factory.CreateBuilder()
            .WithLanguage(_language)
            .Build();

        var segments = new List<string>();
        await foreach (var segment in processor.ProcessAsync(wavStream))
        {
            segments.Add(segment.Text);
        }

        return string.Join(" ", segments).Trim();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _factory?.Dispose();
    }
}
