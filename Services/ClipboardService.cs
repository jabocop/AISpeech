namespace AISpeech.Services;

public sealed class ClipboardService
{
    private readonly Control _marshalControl;

    public ClipboardService(Control marshalControl)
    {
        _marshalControl = marshalControl;
    }

    public Task SetTextAsync(string text)
    {
        var tcs = new TaskCompletionSource();
        _marshalControl.BeginInvoke(() =>
        {
            try
            {
                Clipboard.SetText(text);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
