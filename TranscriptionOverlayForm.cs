using System.Runtime.InteropServices;

namespace AISpeech;

public sealed class TranscriptionOverlayForm : Form
{
    private const int SW_SHOWNOACTIVATE = 4;

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private readonly Label _label;

    public TranscriptionOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoSize = false;
        Size = new Size(400, 80);
        BackColor = Color.FromArgb(30, 30, 30);
        Padding = new Padding(10);
        Opacity = 0.92;

        _label = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Consolas", 10f),
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Controls.Add(_label);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x00000080;
            const int WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    public void UpdateText(string text)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            BeginInvoke(() => UpdateText(text));
            return;
        }

        _label.Text = text;
    }

    public void ShowNear(Point screenPoint)
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            BeginInvoke(() => ShowNear(screenPoint));
            return;
        }

        var screen = Screen.FromPoint(screenPoint).WorkingArea;
        var x = screenPoint.X + 10;
        var y = screenPoint.Y + 10;

        // Clamp to screen working area
        if (x + Width > screen.Right)
            x = screen.Right - Width;
        if (y + Height > screen.Bottom)
            y = screen.Bottom - Height;
        if (x < screen.Left)
            x = screen.Left;
        if (y < screen.Top)
            y = screen.Top;

        Location = new Point(x, y);

        if (!Visible)
            ShowWindow(Handle, SW_SHOWNOACTIVATE);
    }

    public void HideOverlay()
    {
        if (IsDisposed) return;

        if (InvokeRequired)
        {
            BeginInvoke(HideOverlay);
            return;
        }

        Hide();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }
}
