namespace ParakeetPtt.App;

internal static class DarkTheme
{
    public static readonly Color Background = Color.FromArgb(24, 26, 30);
    public static readonly Color Surface = Color.FromArgb(34, 37, 43);
    public static readonly Color SurfaceRaised = Color.FromArgb(44, 48, 56);
    public static readonly Color Text = Color.FromArgb(238, 241, 245);
    public static readonly Color MutedText = Color.FromArgb(164, 171, 181);
    public static readonly Color Accent = Color.FromArgb(91, 141, 239);
    public static readonly Color Danger = Color.FromArgb(221, 86, 86);
    public static readonly Color Border = Color.FromArgb(62, 67, 77);

    public static Font HeaderFont => new("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point);
    public static Font BodyFont => new("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

    public static void Apply(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        form.Font = BodyFont;
        form.StartPosition = FormStartPosition.CenterScreen;
    }

    public static void Apply(Control control)
    {
        control.ForeColor = Text;
        control.BackColor = control is TextBoxBase or ComboBox or ListBox ? SurfaceRaised : Background;
        control.Font = BodyFont;

        foreach (Control child in control.Controls)
        {
            Apply(child);
        }
    }

    public static Button Button(string text)
    {
        return new Button
        {
            Text = text,
            AutoSize = false,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceRaised,
            ForeColor = Text,
            UseVisualStyleBackColor = false
        };
    }

    public static Label Label(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = MutedText,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 6, 0, 3)
        };
    }
}
