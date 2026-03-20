using AISpeech.Services;
using FluentAssertions;
using Xunit;

namespace AISpeech.Tests.Services;

public class SoundServiceTests
{
    [Fact]
    public void PlayRecordStart_WhenDisabled_DoesNotThrow()
    {
        var settings = new AppSettings { SoundOnRecordStart = false };
        using var service = new SoundService(settings);

        var act = () => service.PlayRecordStart();

        act.Should().NotThrow();
    }

    [Fact]
    public void PlayRecordStop_WhenDisabled_DoesNotThrow()
    {
        var settings = new AppSettings { SoundOnRecordStop = false };
        using var service = new SoundService(settings);

        var act = () => service.PlayRecordStop();

        act.Should().NotThrow();
    }

    [Fact]
    public void PlayTranscriptionComplete_WhenDisabled_DoesNotThrow()
    {
        var settings = new AppSettings { SoundOnTranscriptionComplete = false };
        using var service = new SoundService(settings);

        var act = () => service.PlayTranscriptionComplete();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WhenNoSoundsPlayed_DoesNotThrow()
    {
        var settings = new AppSettings();
        var service = new SoundService(settings);

        var act = () => service.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var settings = new AppSettings();
        var service = new SoundService(settings);

        service.Dispose();
        var act = () => service.Dispose();

        act.Should().NotThrow();
    }
}
