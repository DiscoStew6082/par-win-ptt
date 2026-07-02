using ParakeetPtt.Core;

namespace ParakeetPtt.App;

internal sealed class StatusOverlayForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;

    private readonly Panel _accent = new();
    private readonly Label _title = new();
    private readonly Label _message = new();
    private readonly System.Windows.Forms.Timer _hideTimer = new();

    public StatusOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        Size = new Size(340, 86);
        MinimumSize = Size;
        MaximumSize = Size;
        Padding = new Padding(0);
        BackColor = DarkTheme.Surface;
        ForeColor = DarkTheme.Text;
        Font = DarkTheme.BodyFont;

        _accent.Dock = DockStyle.Left;
        _accent.Width = 6;
        _accent.BackColor = DarkTheme.Accent;

        _title.AutoSize = false;
        _title.Dock = DockStyle.Top;
        _title.Height = 28;
        _title.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point);
        _title.ForeColor = DarkTheme.Text;
        _title.BackColor = Color.Transparent;
        _title.TextAlign = ContentAlignment.BottomLeft;

        _message.AutoSize = false;
        _message.Dock = DockStyle.Fill;
        _message.Font = DarkTheme.BodyFont;
        _message.ForeColor = DarkTheme.MutedText;
        _message.BackColor = Color.Transparent;
        _message.TextAlign = ContentAlignment.TopLeft;

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 8, 14, 10),
            BackColor = DarkTheme.Surface
        };
        content.Controls.Add(_message);
        content.Controls.Add(_title);

        Controls.Add(content);
        Controls.Add(_accent);

        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };
    }

    protected override bool ShowWithoutActivation => true;

    internal static int NoActivateExtendedStyleForTest => WsExNoActivate;

    internal bool ShowWithoutActivationForTest => ShowWithoutActivation;

    internal int ExtendedWindowStyleForTest => CreateParams.ExStyle;

    internal bool AutoHideTimerEnabledForTest => _hideTimer.Enabled;

    internal string TitleTextForTest => _title.Text;

    internal string MessageTextForTest => _message.Text;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoActivate | WsExToolWindow;
            return cp;
        }
    }

    public void ShowStatus(DictationStatus status)
    {
        _hideTimer.Stop();
        ApplyStatus(status);
        PositionNearTray();

        if (!Visible)
        {
            Show();
        }

        StartAutoHideIfNeeded(status);
    }

    internal void ApplyStatusForTest(DictationStatus status)
    {
        _hideTimer.Stop();
        ApplyStatus(status);
        StartAutoHideIfNeeded(status);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
            _title.Dispose();
            _message.Dispose();
            _accent.Dispose();
        }

        base.Dispose(disposing);
    }

    private static Color AccentFor(DictationStatusKind kind)
    {
        return kind switch
        {
            DictationStatusKind.Listening => DarkTheme.Accent,
            DictationStatusKind.Transcribing => Color.FromArgb(245, 171, 64),
            DictationStatusKind.Pasted => Color.FromArgb(88, 180, 120),
            DictationStatusKind.EmptyTranscript => DarkTheme.MutedText,
            DictationStatusKind.Error => DarkTheme.Danger,
            _ => DarkTheme.Accent
        };
    }

    private void ApplyStatus(DictationStatus status)
    {
        _accent.BackColor = AccentFor(status.Kind);
        _title.Text = status.Title;
        _message.Text = status.Message;
    }

    private void StartAutoHideIfNeeded(DictationStatus status)
    {
        if (!status.AutoHide)
        {
            return;
        }

        _hideTimer.Interval = status.Kind == DictationStatusKind.Error ? 3500 : 1500;
        _hideTimer.Start();
    }

    private void PositionNearTray()
    {
        var area = Screen.GetWorkingArea(Cursor.Position);
        Location = new Point(
            Math.Max(area.Left + 12, area.Right - Width - 20),
            Math.Max(area.Top + 12, area.Bottom - Height - 20));
    }
}
