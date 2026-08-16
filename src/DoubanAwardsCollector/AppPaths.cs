namespace DoubanAwardsCollector;

internal static class AppPaths
{
    public static string Root { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DoubanAwardsCollector");

    public static string DataFolder { get; } = Path.Combine(Root, "data");
    public static string JsonFolder { get; } = Path.Combine(Root, "json");
    public static string LogsFolder { get; } = Path.Combine(Root, "logs");
    public static string WebView2UserDataFolder { get; } = Path.Combine(Root, "webview2");
    public static string DatabasePath { get; } = Path.Combine(DataFolder, "awards.db");

    public static string ParserScriptPath =>
        Path.Combine(AppContext.BaseDirectory, "Assets", "award-parser.js");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(JsonFolder);
        Directory.CreateDirectory(LogsFolder);
        Directory.CreateDirectory(WebView2UserDataFolder);
    }
}
