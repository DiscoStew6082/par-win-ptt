using ParakeetPtt.Core;

namespace ParakeetPtt.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly ModelRegistry _modelRegistry = ModelRegistry.CreateDefault();
    private readonly SessionHistory _history = new();
    private readonly AppSettingsStore _settingsStore = new(AppPaths.SettingsPath);
    private readonly DictationController _dictationController;
    private readonly NotifyIcon _notifyIcon;
    private readonly RightCtrlHotkeySource _hotkeySource;
    private SettingsForm? _settingsForm;
    private SessionHistoryForm? _historyForm;
    private AppSettings _settings = AppSettings.Default;
    private bool _exiting;

    public TrayApplicationContext()
    {
        _dictationController = new DictationController(
            new WaveInAudioRecorder(AppPaths.RootDirectory),
            new LazyAssetTranscriber(
                AppPaths.RootDirectory,
                _settingsStore,
                () => _settings,
                settings => _settings = settings,
                message => ShowTrayNotification("Parakeet PTT", message, ToolTipIcon.Info)),
            new ClipboardPaster(),
            _history);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Parakeet PTT",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => ShowSettings();
        _hotkeySource = new RightCtrlHotkeySource();
        _hotkeySource.Pressed += () => _ = OnHotkeyPressedAsync();
        _hotkeySource.Released += () => _ = OnHotkeyReleasedAsync();
        _hotkeySource.Start();
        _ = LoadSettingsAsync();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip
        {
            BackColor = DarkTheme.Surface,
            ForeColor = DarkTheme.Text,
            Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable())
        };

        menu.Items.Add(new ToolStripMenuItem("Settings", null, (_, _) => ShowSettings()));
        menu.Items.Add(new ToolStripMenuItem("Session History", null, (_, _) => ShowHistory()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApplication()));

        return menu;
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            _settings = await _settingsStore.LoadAsync(CancellationToken.None);
            UpdateTrayText();
        }
        catch (Exception ex)
        {
            ShowTrayNotification("Settings were not loaded", ex.Message, ToolTipIcon.Warning);
        }
    }

    private void ShowSettings()
    {
        _settingsForm ??= CreateSettingsForm();
        _ = ShowSettingsAsync(_settingsForm);
    }

    private SettingsForm CreateSettingsForm()
    {
        var form = new SettingsForm(_settingsStore, _modelRegistry);
        form.SettingsSaved += (_, settings) =>
        {
            _settings = settings;
            UpdateTrayText();
            ShowTrayNotification("Settings saved", "Parakeet PTT settings were updated.", ToolTipIcon.Info);
        };
        return form;
    }

    private async Task ShowSettingsAsync(SettingsForm form)
    {
        await form.LoadSettingsAsync(CancellationToken.None);
        form.Show();
        form.Activate();
    }

    private void ShowHistory()
    {
        _historyForm ??= new SessionHistoryForm(_history);
        _historyForm.RefreshItems();
        _historyForm.Show();
        _historyForm.Activate();
    }

    private async Task OnHotkeyPressedAsync()
    {
        if (_exiting)
        {
            return;
        }

        ShowTrayNotification("Listening", "Release Right Ctrl to transcribe.", ToolTipIcon.Info);
        await _dictationController.HandleHotkeyDownAsync(CancellationToken.None);
    }

    private async Task OnHotkeyReleasedAsync()
    {
        if (_exiting)
        {
            return;
        }

        ShowTrayNotification("Transcribing", "Sending audio to local parakeet-cli.", ToolTipIcon.Info);
        try
        {
            await _dictationController.HandleHotkeyUpAsync(CancellationToken.None);
            _historyForm?.RefreshItems();
            ShowTrayNotification("Pasted", "Transcript pasted into the active app.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            ShowTrayNotification("Dictation failed", ex.Message, ToolTipIcon.Error);
        }
    }

    private void UpdateTrayText()
    {
        var model = _modelRegistry.Find(_settings.SelectedModelId) ?? _modelRegistry.DefaultModel;
        var text = $"Parakeet PTT - {model.DisplayName}";
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }

    private void ShowTrayNotification(string title, string message, ToolTipIcon icon)
    {
        if (!_settings.NotificationsEnabled)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(2500);
    }

    private void ExitApplication()
    {
        _exiting = true;
        _hotkeySource.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _settingsForm?.Dispose();
        _historyForm?.Dispose();
        ExitThread();
    }
}

internal sealed class DarkMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => DarkTheme.Surface;
    public override Color ImageMarginGradientBegin => DarkTheme.Surface;
    public override Color ImageMarginGradientMiddle => DarkTheme.Surface;
    public override Color ImageMarginGradientEnd => DarkTheme.Surface;
    public override Color MenuItemSelected => DarkTheme.SurfaceRaised;
    public override Color MenuItemSelectedGradientBegin => DarkTheme.SurfaceRaised;
    public override Color MenuItemSelectedGradientEnd => DarkTheme.SurfaceRaised;
    public override Color MenuItemBorder => DarkTheme.Border;
    public override Color SeparatorDark => DarkTheme.Border;
    public override Color SeparatorLight => DarkTheme.Border;
}
