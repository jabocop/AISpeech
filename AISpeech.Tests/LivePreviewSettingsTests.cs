using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AISpeech.Tests;

public class LivePreviewSettingsTests
{
    [Fact]
    public void LivePreviewEnabled_DefaultsToFalse()
    {
        var settings = new AppSettings();

        settings.LivePreviewEnabled.Should().BeFalse();
    }

    [Fact]
    public void LivePreviewIntervalMs_DefaultsTo2500()
    {
        var settings = new AppSettings();

        settings.LivePreviewIntervalMs.Should().Be(2500);
    }

    [Fact]
    public void LivePreviewEnabled_CanBeSetToTrue()
    {
        var settings = new AppSettings { LivePreviewEnabled = true };

        settings.LivePreviewEnabled.Should().BeTrue();
    }

    [Fact]
    public void LivePreviewIntervalMs_CanBeCustomized()
    {
        var settings = new AppSettings { LivePreviewIntervalMs = 1500 };

        settings.LivePreviewIntervalMs.Should().Be(1500);
    }

    [Fact]
    public void LivePreviewEnabled_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISpeech:LivePreviewEnabled"] = "true"
            })
            .Build();

        var settings = new AppSettings();
        config.GetSection("AISpeech").Bind(settings);

        settings.LivePreviewEnabled.Should().BeTrue();
    }

    [Fact]
    public void LivePreviewIntervalMs_BindsFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISpeech:LivePreviewIntervalMs"] = "1000"
            })
            .Build();

        var settings = new AppSettings();
        config.GetSection("AISpeech").Bind(settings);

        settings.LivePreviewIntervalMs.Should().Be(1000);
    }

    [Fact]
    public void LivePreviewSettings_KeepDefaults_WhenNotInConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AISpeech:Language"] = "sv"
            })
            .Build();

        var settings = new AppSettings();
        config.GetSection("AISpeech").Bind(settings);

        settings.LivePreviewEnabled.Should().BeFalse();
        settings.LivePreviewIntervalMs.Should().Be(2500);
    }
}
