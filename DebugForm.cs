namespace AISpeech;

public sealed class DebugForm : Form
{
    private readonly RichTextBox _logBox;

    public DebugForm()
    {
        Text = "AISpeech — Debug";
        Width = 600;
        Height = 400;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;

        _logBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Consolas", 10f),
            WordWrap = true
        };

        Controls.Add(_logBox);
    }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            BeginInvoke(() => Log(message, level));
            return;
        }

        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var prefix = level switch
        {
            LogLevel.Error => "ERR",
            LogLevel.Warn => "WRN",
            _ => "INF"
        };

        var color = level switch
        {
            LogLevel.Error => Color.Tomato,
            LogLevel.Warn => Color.Gold,
            _ => Color.FromArgb(220, 220, 220)
        };

        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = Color.Gray;
        _logBox.AppendText($"[{timestamp}] ");
        _logBox.SelectionStart = _logBox.TextLength;
        _logBox.SelectionColor = color;
        _logBox.AppendText($"{prefix}: {message}{Environment.NewLine}");
        _logBox.ScrollToCaret();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Hide instead of close — app lifecycle is managed by TrayApplicationContext
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    public enum LogLevel { Info, Warn, Error }
}
