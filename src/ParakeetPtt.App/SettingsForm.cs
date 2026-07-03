using ParakeetPtt.Core;
using System.ComponentModel;

namespace ParakeetPtt.App;

internal sealed class SettingsForm : Form
{
    private readonly AppSettingsStore _settingsStore;
    private readonly ModelRegistry _modelRegistry;
    private readonly ComboBox _model = new();
    private readonly TextBox _hotkey = new();
    private readonly TextBox _runtimePath = new();
    private readonly TextBox _modelPath = new();
    private readonly ComboBox _mode = new();
    private readonly ComboBox _device = new();
    private readonly CheckBox _notifications = new();
    private readonly CheckBox _sounds = new();
    private AppSettings _settings = AppSettings.Default;

    public event EventHandler<AppSettings>? SettingsSaved;
    public event EventHandler? QuitRequested;

    public SettingsForm(AppSettingsStore settingsStore, ModelRegistry modelRegistry)
    {
        _settingsStore = settingsStore;
        _modelRegistry = modelRegistry;

        Text = "Parakeet PTT - Settings";
        MinimumSize = new Size(720, 680);
        Size = new Size(760, 720);

        DarkTheme.Apply(this);
        BuildLayout();
    }

    public async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        _settings = await _settingsStore.LoadAsync(cancellationToken);
        UseSettings(_settings);
    }

    public void UseSettings(AppSettings settings)
    {
        _settings = settings;
        ApplySettings(settings);
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20),
            BackColor = DarkTheme.Background,
            AutoScroll = true
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Parakeet PTT Settings",
            AutoSize = true,
            Font = DarkTheme.HeaderFont,
            ForeColor = DarkTheme.Text,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 6)
        };

        var summary = new Label
        {
            Text = "Hold Right Ctrl to record. Runtime and model paths may stay blank; Parakeet PTT will download them on first dictation.",
            AutoSize = false,
            Height = 44,
            Dock = DockStyle.Top,
            ForeColor = DarkTheme.MutedText,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 16)
        };

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            BackColor = DarkTheme.Background
        };

        _hotkey.Dock = DockStyle.Top;
        _hotkey.PlaceholderText = "RightCtrl";

        _model.Dock = DockStyle.Top;
        _model.DropDownStyle = ComboBoxStyle.DropDownList;
        _model.DisplayMember = nameof(ModelInfo.DisplayName);
        _model.Items.AddRange(_modelRegistry.Models.Cast<object>().ToArray());
        _model.SelectedIndexChanged += (_, _) => RefreshModeOptions(SelectedModelFromControl());

        _mode.Dock = DockStyle.Top;
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;

        _runtimePath.Dock = DockStyle.Fill;
        _runtimePath.PlaceholderText = "Auto-download on first dictation";

        _modelPath.Dock = DockStyle.Fill;
        _modelPath.PlaceholderText = "Auto-download on first dictation";

        _device.Dock = DockStyle.Top;
        _device.DropDownStyle = ComboBoxStyle.DropDownList;
        _device.Items.AddRange(Enum.GetValues<DevicePreference>().Cast<object>().ToArray());

        _notifications.Text = "Show tray notifications";
        _notifications.AutoSize = true;
        _notifications.BackColor = Color.Transparent;
        _notifications.ForeColor = DarkTheme.Text;
        _notifications.Margin = new Padding(0, 12, 0, 8);
        _sounds.Text = "Play sounds when recording starts, transcription begins, and paste completes";
        _sounds.AutoSize = true;
        _sounds.BackColor = Color.Transparent;
        _sounds.ForeColor = DarkTheme.Text;
        _sounds.Margin = new Padding(0, 4, 0, 8);

        AddField(fields, "Push-to-talk hotkey", _hotkey);
        AddField(fields, "Model", _model);
        AddField(fields, "Transcription mode", _mode);
        AddPathField(fields, "Runtime path (optional)", _runtimePath, "Blank uses %LOCALAPPDATA%\\ParakeetPtt\\runtimes and downloads the selected runtime automatically.");
        AddPathField(fields, "Model path (optional)", _modelPath, "Blank uses %LOCALAPPDATA%\\ParakeetPtt\\models and downloads the selected model automatically.");
        AddField(fields, "Device preference", _device);
        fields.Controls.Add(_notifications);
        fields.Controls.Add(_sounds);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            BackColor = DarkTheme.Background
        };

        var save = DarkTheme.Button("Save");
        save.Width = 96;
        save.BackColor = DarkTheme.Accent;
        save.Click += async (_, _) => await SaveAsync();

        var quit = DarkTheme.Button("Quit App");
        quit.Width = 96;
        quit.BackColor = DarkTheme.Danger;
        quit.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        var cancel = DarkTheme.Button("Cancel");
        cancel.Width = 96;
        cancel.Click += (_, _) => Hide();

        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(quit);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 1,
            AutoSize = true,
            BackColor = DarkTheme.Background
        };
        header.Controls.Add(title);
        header.Controls.Add(summary);

        root.Controls.Add(header, 0, 0);
        root.Controls.Add(fields, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);

        DarkTheme.Apply(root);
    }

    private static void AddField(TableLayoutPanel fields, string labelText, Control control)
    {
        fields.Controls.Add(DarkTheme.Label(labelText));
        control.Height = 30;
        fields.Controls.Add(control);
    }

    private static void AddPathField(TableLayoutPanel fields, string labelText, TextBox textBox, string helpText)
    {
        fields.Controls.Add(DarkTheme.Label(labelText));

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Height = 34,
            BackColor = DarkTheme.Background,
            Margin = new Padding(0, 0, 0, 2)
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));

        var browse = DarkTheme.Button("Browse");
        browse.Dock = DockStyle.Fill;
        browse.Margin = new Padding(8, 0, 0, 0);
        browse.Click += (_, _) =>
        {
            using var dialog = new OpenFileDialog
            {
                CheckFileExists = false,
                FileName = textBox.Text
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox.Text = dialog.FileName;
            }
        };

        textBox.Dock = DockStyle.Fill;
        row.Controls.Add(textBox, 0, 0);
        row.Controls.Add(browse, 1, 0);
        fields.Controls.Add(row);
        fields.Controls.Add(DarkTheme.HelpText(helpText));
    }

    private void ApplySettings(AppSettings settings)
    {
        _hotkey.Text = settings.Hotkey;
        _runtimePath.Text = settings.RuntimePath ?? string.Empty;
        _modelPath.Text = settings.ModelPath ?? string.Empty;
        _device.SelectedItem = settings.DevicePreference;
        _mode.SelectedItem = settings.TranscriptionMode;
        _notifications.Checked = settings.NotificationsEnabled;
        _sounds.Checked = settings.AudibleStatusEnabled;

        var selected = _modelRegistry.Find(settings.SelectedModelId) ?? _modelRegistry.DefaultModel;
        _model.SelectedItem = selected;
        RefreshModeOptions(selected);
        _mode.SelectedItem = ModeSupportedByModel(settings.TranscriptionMode, selected)
            ? settings.TranscriptionMode
            : TranscriptionMode.Auto;
    }

    private async Task SaveAsync()
    {
        _settings = BuildSettingsFromControls();

        await _settingsStore.SaveAsync(_settings, CancellationToken.None);
        SettingsSaved?.Invoke(this, _settings);
        Hide();
    }

    private AppSettings BuildSettingsFromControls()
    {
        var selectedModelId = SelectedModelIdFromControl();
        var selectedModel = SelectedModelFromControl();
        var requestedMode = _mode.SelectedItem is TranscriptionMode mode ? mode : TranscriptionMode.Auto;
        var selectedMode = ModeSupportedByModel(requestedMode, selectedModel)
            ? requestedMode
            : TranscriptionMode.Auto;
        var modelPath = EmptyToNull(_modelPath.Text);
        if (!string.Equals(selectedModelId, _settings.SelectedModelId, StringComparison.OrdinalIgnoreCase)
            && IsPreviousAutoModelPath(modelPath))
        {
            modelPath = null;
        }

        return _settings with
        {
            Hotkey = string.IsNullOrWhiteSpace(_hotkey.Text) ? AppSettings.Default.Hotkey : _hotkey.Text.Trim(),
            SelectedModelId = selectedModelId,
            TranscriptionMode = selectedMode,
            RuntimePath = EmptyToNull(_runtimePath.Text),
            ModelPath = modelPath,
            DevicePreference = _device.SelectedItem is DevicePreference preference ? preference : DevicePreference.Cuda,
            NotificationsEnabled = _notifications.Checked,
            AudibleStatusEnabled = _sounds.Checked
        };
    }

    private string SelectedModelIdFromControl()
    {
        return SelectedModelFromControl().Id;
    }

    private ModelInfo SelectedModelFromControl()
    {
        return _model.SelectedItem is ModelInfo model ? model : _modelRegistry.DefaultModel;
    }

    private void RefreshModeOptions(ModelInfo model)
    {
        var selected = _mode.SelectedItem is TranscriptionMode mode ? mode : TranscriptionMode.Auto;
        _mode.Items.Clear();
        _mode.Items.Add(TranscriptionMode.Auto);
        if (model.SupportsBatch)
        {
            _mode.Items.Add(TranscriptionMode.Batch);
        }

        if (model.SupportsStreaming)
        {
            _mode.Items.Add(TranscriptionMode.Streaming);
        }

        _mode.SelectedItem = ModeSupportedByModel(selected, model) ? selected : TranscriptionMode.Auto;
    }

    private static bool ModeSupportedByModel(TranscriptionMode mode, ModelInfo model)
    {
        return mode switch
        {
            TranscriptionMode.Batch => model.SupportsBatch,
            TranscriptionMode.Streaming => model.SupportsStreaming,
            _ => true
        };
    }

    private bool IsPreviousAutoModelPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var previousModel = _modelRegistry.Find(_settings.SelectedModelId);
        return previousModel is not null
            && string.Equals(
                Path.GetFileName(path),
                Path.GetFileName(previousModel.DownloadUrl.LocalPath),
                StringComparison.OrdinalIgnoreCase);
    }

    private static string? EmptyToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal TranscriptionMode SelectedTranscriptionModeForTest
    {
        get => _mode.SelectedItem is TranscriptionMode mode ? mode : TranscriptionMode.Auto;
        set
        {
            if (!_mode.Items.Contains(value))
            {
                _mode.Items.Add(value);
            }

            _mode.SelectedItem = value;
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal string SelectedModelIdForTest
    {
        get => SelectedModelIdFromControl();
        set => _model.SelectedItem = _modelRegistry.Find(value) ?? _modelRegistry.DefaultModel;
    }

    internal void SaveForTest()
    {
        SaveAsync().GetAwaiter().GetResult();
    }

    internal AppSettings BuildSettingsForTest()
    {
        return BuildSettingsFromControls();
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
