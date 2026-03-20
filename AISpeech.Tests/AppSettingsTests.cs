using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AISpeech.Tests;

public class AppSettingsTests
{
    [Fact]
    public void AutoPasteAtCursor_DefaultsToFalse()
    {
        var settings = new AppSettings();

        settings.AutoPasteAtCursor.Should().BeFalse();
    }

    [Fact]
    public void AutoPasteAtCursor_CanBeSetToTrue()
    {
        var settings = new AppSettings { AutoPasteAtCursor = true };

        settings.AutoPasteAtCursor.Should().BeTrue();
    }

    [Fact]
    public void AutoPasteAtCursor_BindsFromConfiguration_WhenTrue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISpeech:AutoPasteAtCursor"] = "true"
            })
            .Build();

        var settings = new AppSettings();
        config.GetSection("AISpeech").Bind(settings);

        settings.AutoPasteAtCursor.Should().BeTrue();
    }

    [Fact]
    public void AutoPasteAtCursor_BindsFromConfiguration_WhenFalse()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISpeech:AutoPasteAtCursor"] = "false"
            })
            .Build();

        var settings = new AppSettings();
        config.GetSection("AISpeech").Bind(settings);

        settings.AutoPasteAtCursor.Should().BeFalse();
    }

    [Fact]
    public void AutoPasteAtCursor_KeepsDefault_WhenNotInConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISpeech:Language"] = "sv"
            })
            .Build();

        var settings = new AppSettings();
        config.GetSection("AISpeech").Bind(settings);

        settings.AutoPasteAtCursor.Should().BeFalse();
    }

    [Fact]
    public void SoundSettings_DefaultToTrue()
    {
        var settings = new AppSettings();

        settings.SoundOnRecordStart.Should().BeTrue();
        settings.SoundOnRecordStop.Should().BeTrue();
        settings.SoundOnTranscriptionComplete.Should().BeTrue();
    }

    [Fact]
    public void SoundSettings_BindFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISpeech:SoundOnRecordStart"] = "false",
                ["AISpeech:SoundOnRecordStop"] = "false",
                ["AISpeech:SoundOnTranscriptionComplete"] = "false"
            })
            .Build();

        var settings = new AppSettings();
        config.GetSection("AISpeech").Bind(settings);

        settings.SoundOnRecordStart.Should().BeFalse();
        settings.SoundOnRecordStop.Should().BeFalse();
        settings.SoundOnTranscriptionComplete.Should().BeFalse();
    }

    [Fact]
    public void SoundSettings_KeepDefaults_WhenNotInConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISpeech:Language"] = "sv"
            })
            .Build();

        var settings = new AppSettings();
        config.GetSection("AISpeech").Bind(settings);

        settings.SoundOnRecordStart.Should().BeTrue();
        settings.SoundOnRecordStop.Should().BeTrue();
        settings.SoundOnTranscriptionComplete.Should().BeTrue();
    }
}
