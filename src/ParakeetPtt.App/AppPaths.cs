namespace ParakeetPtt.App;

internal static class AppPaths
{
    public static string SettingsPath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "ParakeetPtt", "settings.json");
        }
    }

    public static string RootDirectory => Path.GetDirectoryName(SettingsPath)
        ?? Path.Combine(Path.GetTempPath(), "ParakeetPtt");
}
