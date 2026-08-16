using System.Text;

namespace DoubanAwardsCollector;

internal static class AppLogger
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        try
        {
            AppPaths.EnsureCreated();
            var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}";
            var path = Path.Combine(AppPaths.LogsFolder, $"collector-{DateTime.Now:yyyyMMdd}.log");

            lock (Gate)
            {
                File.AppendAllText(path, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
        }
    }
}
