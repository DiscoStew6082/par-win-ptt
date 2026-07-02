using ParakeetPtt.Core;

namespace ParakeetPtt.App;

internal sealed class StatusOverlayForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private static readonly Size DefaultOverlaySize = new(560, 144);

    private readonly Panel _accent = new();
    private readonly Label _title = new();
    private readonly Label _message = new();
    private readonly System.Windows.Forms.Timer _hideTimer = new();

    public StatusOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Size = DefaultOverlaySize;
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
        _title.Height = 36;
        _title.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point);
        _title.ForeColor = DarkTheme.Text;
        _title.BackColor = Color.Transparent;
        _title.TextAlign = ContentAlignment.MiddleLeft;

        _message.AutoSize = false;
        _message.Dock = DockStyle.Fill;
        _message.Font = DarkTheme.BodyFont;
        _message.ForeColor = DarkTheme.MutedText;
        _message.BackColor = Color.Transparent;
        _message.TextAlign = ContentAlignment.MiddleLeft;
        _message.AutoEllipsis = true;

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 14),
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

    internal static Size DefaultSizeForTest => DefaultOverlaySize;

    internal bool ShowWithoutActivationForTest => ShowWithoutActivation;

    internal int ExtendedWindowStyleForTest => CreateParams.ExStyle;

    internal bool AutoHideTimerEnabledForTest => _hideTimer.Enabled;

    internal string TitleTextForTest => _title.Text;

    internal string MessageTextForTest => _message.Text;

    internal ContentAlignment TitleAlignmentForTest => _title.TextAlign;

    internal ContentAlignment MessageAlignmentForTest => _message.TextAlign;

    internal int TitleHeightForTest => _title.Height;

    internal int MessageHeightForTest => _message.Height;

    internal int TitlePreferredHeightForTest => _title.GetPreferredSize(new Size(_title.Width, 0)).Height;

    internal int MessagePreferredHeightForTest => _message.GetPreferredSize(new Size(_message.Width, 0)).Height;

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
        PositionBottomCenter();

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
            DictationStatusKind.TranscriptPreview => Color.FromArgb(88, 180, 120),
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

    private void PositionBottomCenter()
    {
        var area = Screen.GetWorkingArea(Cursor.Position);
        Location = CalculateBottomCenterLocation(area, Size);
    }

    internal static Point CalculateBottomCenterLocationForTest(Rectangle workingArea, Size overlaySize)
    {
        return CalculateBottomCenterLocation(workingArea, overlaySize);
    }

    private static Point CalculateBottomCenterLocation(Rectangle workingArea, Size overlaySize)
    {
        const int margin = 20;
        var centeredX = workingArea.Left + (workingArea.Width - overlaySize.Width) / 2;
        var minX = workingArea.Left + margin;
        var maxX = workingArea.Right - overlaySize.Width - margin;
        var x = maxX < minX ? minX : Math.Clamp(centeredX, minX, maxX);
        return new Point(
            x,
            Math.Max(workingArea.Top + margin, workingArea.Bottom - overlaySize.Height - margin));
    }
}
