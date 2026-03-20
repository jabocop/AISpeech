using Microsoft.Toolkit.Uwp.Notifications;

namespace AISpeech.Services;

public static class NotificationService
{
    public static void ShowSuccess(string text)
    {
        var preview = text.Length > 80 ? text[..80] + "..." : text;
        new ToastContentBuilder()
            .AddText("Transcription Complete")
            .AddText(preview)
            .Show();
    }

    public static void ShowError(string message)
    {
        new ToastContentBuilder()
            .AddText("AISpeech Error")
            .AddText(message)
            .Show();
    }

    public static void ShowInfo(string message)
    {
        new ToastContentBuilder()
            .AddText("AISpeech")
            .AddText(message)
            .Show();
    }
}
