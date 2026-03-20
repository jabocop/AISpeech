using AISpeech.Services;
using FluentAssertions;
using Xunit;

namespace AISpeech.Tests.Services;

public class HotkeyManagerServiceTests
{
    private static AppSettings CreateSettings(string modifiers, string key = "")
        => new() { HotkeyModifiers = modifiers, HotkeyKey = key };

    [Theory]
    [InlineData("Ctrl+Win")]
    [InlineData("Shift+Alt")]
    [InlineData("Ctrl+Shift")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Win+Shift")]
    [InlineData("Ctrl+Shift+Alt")]
    public void Constructor_ValidModifierOnlyBindings_DoNotThrow(string modifiers)
    {
        var act = () =>
        {
            using var service = new HotkeyManagerService(CreateSettings(modifiers));
        };

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Ctrl", "A")]
    [InlineData("Shift", "F1")]
    [InlineData("Alt", "Space")]
    public void Constructor_SingleModifierWithKey_DoesNotThrow(string modifiers, string key)
    {
        var act = () =>
        {
            using var service = new HotkeyManagerService(CreateSettings(modifiers, key));
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_SingleModifierOnly_ThrowsRequiresAtLeastTwo()
    {
        var act = () =>
        {
            using var service = new HotkeyManagerService(CreateSettings("Ctrl"));
        };

        act.Should().Throw<ArgumentException>().WithMessage("*at least 2 modifiers*");
    }

    [Fact]
    public void Constructor_UnknownModifier_Throws()
    {
        var act = () =>
        {
            using var service = new HotkeyManagerService(CreateSettings("Ctrl+Banana"));
        };

        act.Should().Throw<ArgumentException>().WithMessage("*Unknown modifier*");
    }

    [Fact]
    public void Constructor_UnknownKey_Throws()
    {
        var act = () =>
        {
            using var service = new HotkeyManagerService(CreateSettings("Ctrl", "NotAKey"));
        };

        act.Should().Throw<ArgumentException>().WithMessage("*Unknown key*");
    }

    [Fact]
    public void Constructor_EmptyModifiers_ThrowsRequiresAtLeastTwo()
    {
        var act = () =>
        {
            using var service = new HotkeyManagerService(CreateSettings(""));
        };

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("ctrl+win")]
    [InlineData("CTRL+WIN")]
    [InlineData("Ctrl+Win")]
    [InlineData("CONTROL+META")]
    public void Constructor_ModifierParsingIsCaseInsensitive(string modifiers)
    {
        var act = () =>
        {
            using var service = new HotkeyManagerService(CreateSettings(modifiers));
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_ModifiersWithExtraSpaces_ParsesCorrectly()
    {
        var act = () =>
        {
            using var service = new HotkeyManagerService(CreateSettings(" Ctrl + Win "));
        };

        act.Should().NotThrow();
    }
}
