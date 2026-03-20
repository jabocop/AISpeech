using AISpeech.Services;
using FluentAssertions;
using Xunit;

namespace AISpeech.Tests.Services;

public class PhraseTriggerServiceTests
{
    private static AppSettings CreateSettings(params (string Phrase, string Mode)[] triggers)
    {
        var settings = new AppSettings
        {
            SpeechTriggers = triggers.Select(t => new SpeechTriggerConfig
            {
                Phrase = t.Phrase,
                Mode = t.Mode
            }).ToList()
        };
        return settings;
    }

    private static PhraseTriggerService CreateService(params (string Phrase, string Mode)[] triggers)
        => new(CreateSettings(triggers));

    [Fact]
    public void Detect_NoTriggerMatch_ReturnsTranscribeAndOriginalText()
    {
        var service = CreateService(("use plan", "Prompt"));

        var (mode, cleaned) = service.Detect("hello world this is a test");

        mode.Should().Be(CaptureMode.Transcribe);
        cleaned.Should().Be("hello world this is a test");
    }

    [Fact]
    public void Detect_MatchesPromptTrigger_ReturnsModeAndRemovesPhrase()
    {
        var service = CreateService(("use plan", "Prompt"));

        var (mode, cleaned) = service.Detect("I want to use plan for building a feature");

        mode.Should().Be(CaptureMode.Prompt);
        cleaned.Should().Be("I want to  for building a feature");
    }

    [Fact]
    public void Detect_MatchesRalphTrigger_ReturnsModeAndRemovesPhrase()
    {
        var service = CreateService(("use ralph", "Ralph"));

        var (mode, cleaned) = service.Detect("use ralph create a task plan for login");

        mode.Should().Be(CaptureMode.Ralph);
        cleaned.Should().Be("create a task plan for login");
    }

    [Fact]
    public void Detect_CaseInsensitive_MatchesRegardlessOfCase()
    {
        var service = CreateService(("use plan", "Prompt"));

        var (mode, _) = service.Detect("I want to USE PLAN for this");

        mode.Should().Be(CaptureMode.Prompt);
    }

    [Fact]
    public void Detect_PhraseAtEnd_RemovesPhraseAndTrims()
    {
        var service = CreateService(("use plan", "Prompt"));

        var (mode, cleaned) = service.Detect("build the login page use plan");

        mode.Should().Be(CaptureMode.Prompt);
        cleaned.Should().Be("build the login page");
    }

    [Fact]
    public void Detect_PhraseIsEntireText_ReturnsEmptyCleanedText()
    {
        var service = CreateService(("use plan", "Prompt"));

        var (mode, cleaned) = service.Detect("use plan");

        mode.Should().Be(CaptureMode.Prompt);
        cleaned.Should().BeEmpty();
    }

    [Fact]
    public void Detect_MultipleTriggers_FirstMatchWins()
    {
        var service = CreateService(("use plan", "Prompt"), ("use ralph", "Ralph"));

        var (mode, _) = service.Detect("use plan and use ralph");

        mode.Should().Be(CaptureMode.Prompt);
    }

    [Fact]
    public void Detect_EmptyInput_ReturnsTranscribe()
    {
        var service = CreateService(("use plan", "Prompt"));

        var (mode, cleaned) = service.Detect("");

        mode.Should().Be(CaptureMode.Transcribe);
        cleaned.Should().BeEmpty();
    }

    [Fact]
    public void Detect_NoTriggers_AlwaysReturnsTranscribe()
    {
        var service = CreateService();

        var (mode, cleaned) = service.Detect("use plan something");

        mode.Should().Be(CaptureMode.Transcribe);
        cleaned.Should().Be("use plan something");
    }

    [Fact]
    public void Constructor_SkipsBlankPhrases()
    {
        var service = CreateService(("", "Prompt"), ("  ", "Ralph"), ("use plan", "Prompt"));

        service.Triggers.Should().HaveCount(1);
        service.Triggers[0].Phrase.Should().Be("use plan");
    }

    [Fact]
    public void Constructor_ThrowsOnUnknownMode()
    {
        var act = () => CreateService(("use plan", "UnknownMode"));

        act.Should().Throw<ArgumentException>().WithMessage("*Unknown capture mode*");
    }

    [Fact]
    public void Constructor_ParsesModesCaseInsensitive()
    {
        var service = CreateService(("use plan", "prompt"), ("use ralph", "RALPH"));

        service.Triggers[0].Mode.Should().Be(CaptureMode.Prompt);
        service.Triggers[1].Mode.Should().Be(CaptureMode.Ralph);
    }

    [Fact]
    public void Detect_PhraseInMiddle_RemovesPhraseAndJoinsCleanly()
    {
        var service = CreateService(("use plan", "Prompt"));

        var (_, cleaned) = service.Detect("please use plan now");

        cleaned.Should().Be("please  now");
    }
}
