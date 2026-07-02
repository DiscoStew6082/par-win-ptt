using ParakeetPtt.Core;

namespace ParakeetPtt.App;

internal sealed class SessionHistoryForm : Form
{
    private readonly SessionHistory _history;
    private readonly ListBox _items = new();

    public event EventHandler? QuitRequested;

    public SessionHistoryForm(SessionHistory history)
    {
        _history = history;

        Text = "Parakeet PTT - Session History";
        MinimumSize = new Size(520, 420);
        Size = new Size(640, 520);

        DarkTheme.Apply(this);
        BuildLayout();
        RefreshItems();
    }

    public void RefreshItems()
    {
        _items.Items.Clear();

        if (_history.Items.Count == 0)
        {
            _items.Items.Add("No transcripts yet.");
            return;
        }

        foreach (var item in _history.Items.Reverse())
        {
            _items.Items.Add(item);
        }
    }

    private void BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18),
            BackColor = DarkTheme.Background
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Session History",
            AutoSize = true,
            Font = DarkTheme.HeaderFont,
            ForeColor = DarkTheme.Text,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 12)
        };

        _items.Dock = DockStyle.Fill;
        _items.BorderStyle = BorderStyle.FixedSingle;
        _items.HorizontalScrollbar = true;
        _items.IntegralHeight = false;
        _items.BackColor = DarkTheme.SurfaceRaised;
        _items.ForeColor = DarkTheme.Text;

        var closeButton = DarkTheme.Button("Close");
        closeButton.Width = 96;
        closeButton.Anchor = AnchorStyles.Right;
        closeButton.Click += (_, _) => Hide();

        var quitButton = DarkTheme.Button("Quit App");
        quitButton.Width = 96;
        quitButton.BackColor = DarkTheme.Danger;
        quitButton.Anchor = AnchorStyles.Right;
        quitButton.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            BackColor = DarkTheme.Background
        };
        buttons.Controls.Add(closeButton);
        buttons.Controls.Add(quitButton);

        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(_items, 0, 1);
        layout.Controls.Add(buttons, 0, 2);

        Controls.Add(layout);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }
}
