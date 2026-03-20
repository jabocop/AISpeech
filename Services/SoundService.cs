using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AISpeech.Services;

public sealed class SoundService : IDisposable
{
    private readonly AppSettings _settings;
    private bool _disposed;

    public SoundService(AppSettings settings)
    {
        _settings = settings;
    }

    public void PlayRecordStart()
    {
        if (!_settings.SoundOnRecordStart) return;
        PlayAsync(CreateTwoTone(330f, 520f));
    }

    public void PlayRecordStop()
    {
        if (!_settings.SoundOnRecordStop) return;
        PlayAsync(CreateTwoTone(520f, 330f));
    }

    public void PlayTranscriptionComplete()
    {
        if (!_settings.SoundOnTranscriptionComplete) return;
        PlayAsync(CreateTriplePip(800f));
    }

    private static ISampleProvider CreateTone(float frequency, double durationSeconds)
    {
        var signal = new SignalGenerator(44100, 1)
        {
            Gain = 0.15,
            Frequency = frequency,
            Type = SignalGeneratorType.Sin
        };
        return signal.Take(TimeSpan.FromSeconds(durationSeconds));
    }

    private static ISampleProvider CreateSilence(double durationSeconds)
    {
        var signal = new SignalGenerator(44100, 1)
        {
            Gain = 0.0,
            Type = SignalGeneratorType.Sin
        };
        return signal.Take(TimeSpan.FromSeconds(durationSeconds));
    }

    private static ISampleProvider CreateTwoTone(float freq1, float freq2)
    {
        return new ConcatenatingSampleProvider(new[]
        {
            CreateTone(freq1, 0.12),
            CreateTone(freq2, 0.12)
        });
    }

    private static ISampleProvider CreateTriplePip(float frequency)
    {
        return new ConcatenatingSampleProvider(new[]
        {
            CreateTone(frequency, 0.07),
            CreateSilence(0.04),
            CreateTone(frequency, 0.07),
            CreateSilence(0.04),
            CreateTone(frequency, 0.07)
        });
    }

    private static void PlayAsync(ISampleProvider provider)
    {
        Task.Run(() =>
        {
            try
            {
                var waveOut = new WaveOutEvent();
                waveOut.PlaybackStopped += (_, _) => waveOut.Dispose();
                waveOut.Init(provider);
                waveOut.Play();
            }
            catch
            {
                // Sound failure should never interrupt the recording workflow
            }
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
