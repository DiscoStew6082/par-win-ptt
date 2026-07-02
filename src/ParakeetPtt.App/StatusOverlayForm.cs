using ParakeetPtt.Core;
using System.Drawing.Drawing2D;

namespace ParakeetPtt.App;

internal sealed class StatusOverlayForm : Form
{
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private static readonly Size DefaultOverlaySize = new(560, 144);

    private readonly Panel _accent = new();
    private readonly Label _title = new();
    private readonly Label _message = new();
    private readonly ActivityMeterControl _activityMeter = new();
    private readonly System.Windows.Forms.Timer _hideTimer = new();
    private readonly System.Windows.Forms.Timer _liveActivityTimer = new();
    private DateTimeOffset _listeningStartedAt;
    private int _activityPhase;
    private bool _activityMeterRequestedVisible;

    public StatusOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        Size = DefaultOverlaySize;
        MinimumSize = Size;
        MaximumSize = Size;
        Padding = new Padding(1);
        BackColor = DarkTheme.Border;
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

        _activityMeter.Dock = DockStyle.Bottom;
        _activityMeter.Height = 24;
        _activityMeter.Visible = false;

        var content = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 12, 16, 14),
            BackColor = DarkTheme.Surface
        };
        content.Controls.Add(_activityMeter);
        content.Controls.Add(_message);
        content.Controls.Add(_title);

        Controls.Add(content);
        Controls.Add(_accent);

        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };

        _liveActivityTimer.Interval = 200;
        _liveActivityTimer.Tick += (_, _) => UpdateLiveActivity();
    }

    protected override bool ShowWithoutActivation => true;

    internal static int NoActivateExtendedStyleForTest => WsExNoActivate;

    internal static Size DefaultSizeForTest => DefaultOverlaySize;

    internal bool ShowWithoutActivationForTest => ShowWithoutActivation;

    internal int ExtendedWindowStyleForTest => CreateParams.ExStyle;

    internal bool AutoHideTimerEnabledForTest => _hideTimer.Enabled;

    internal bool LiveActivityTimerEnabledForTest => _liveActivityTimer.Enabled;

    internal bool ActivityMeterVisibleForTest => _activityMeterRequestedVisible;

    internal double LatestActivityLevelForTest => _activityMeter.Level;

    internal bool HasActivityHistoryForTest => _activityMeter.HasHistory;

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

    internal void UpdateActivityLevelForTest(double level)
    {
        UpdateActivityLevel(level);
    }

    public void UpdateActivityLevel(double level)
    {
        if (!_activityMeterRequestedVisible || IsDisposed)
        {
            return;
        }

        _activityMeter.Level = level;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
            _liveActivityTimer.Dispose();
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
        if (status.Kind == DictationStatusKind.Listening)
        {
            StartLiveActivity(status);
            return;
        }

        StopLiveActivity();
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

    private void StartLiveActivity(DictationStatus status)
    {
        _accent.BackColor = AccentFor(status.Kind);
        _title.Text = status.Title;
        _listeningStartedAt = DateTimeOffset.UtcNow;
        _activityPhase = 0;
        _activityMeterRequestedVisible = true;
        _activityMeter.Visible = true;
        _activityMeter.Reset();
        UpdateLiveActivity();
        _liveActivityTimer.Start();
    }

    private void StopLiveActivity()
    {
        _liveActivityTimer.Stop();
        _activityMeterRequestedVisible = false;
        _activityMeter.Visible = false;
    }

    private void UpdateLiveActivity()
    {
        _message.Text = ListeningStatusFormatter.Format(DateTimeOffset.UtcNow - _listeningStartedAt);
        _activityMeter.Phase = _activityPhase++;
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

    private sealed class ActivityMeterControl : Control
    {
        private readonly double[] _levels = new double[18];
        private int _phase;
        private double _level;

        public ActivityMeterControl()
        {
            DoubleBuffered = true;
            BackColor = DarkTheme.Surface;
        }

        public int Phase
        {
            get => _phase;
            set
            {
                _phase = value;
                Invalidate();
            }
        }

        public double Level
        {
            get => _level;
            set
            {
                _level = Math.Clamp(value, 0, 1);
                _levels[_phase % _levels.Length] = _level;
                Invalidate();
            }
        }

        public bool HasHistory => _levels.Any(level => level > 0);

        public void Reset()
        {
            Array.Clear(_levels);
            _level = 0;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            const int barCount = 18;
            const int gap = 5;
            var availableWidth = Math.Max(1, Width - (barCount - 1) * gap);
            var barWidth = Math.Max(4, availableWidth / barCount);
            var maxHeight = Math.Max(8, Height - 6);
            var yCenter = Height / 2;

            using var inactive = new SolidBrush(Color.FromArgb(64, DarkTheme.MutedText));
            using var active = new SolidBrush(DarkTheme.Accent);

            for (var i = 0; i < barCount; i++)
            {
                var index = (_phase + i) % _levels.Length;
                var age = (_levels.Length - i) / (double)_levels.Length;
                var level = Math.Max(_levels[index] * age, _level * 0.18);
                var barHeight = Math.Max(5, (int)(6 + level * (maxHeight - 6)));
                var x = i * (barWidth + gap);
                var y = yCenter - barHeight / 2;
                var brush = i % 3 == _phase % 3 ? active : inactive;
                FillRoundedRectangle(e.Graphics, brush, new Rectangle(x, y, barWidth, barHeight), 3);
            }
        }

        private static void FillRoundedRectangle(Graphics graphics, Brush brush, Rectangle bounds, int radius)
        {
            var diameter = radius * 2;
            using var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.FillPath(brush, path);
        }
    }
}
